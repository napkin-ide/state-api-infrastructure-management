using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.Core.Exceptions;
using Fathym;
using Fathym.API;
using LCU.Personas.Client.Enterprises;
using LCU.Presentation.State.ReqRes;
using LCU.StateAPI;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace LCU.State.API.NapkinIDE.InfrastructureManagement
{
    public class RenewalEnvironment
    {
        public virtual string EnvironmentLookup { get; set; }

        public virtual string EnterpriseAPIKey { get; set; }

        public virtual string Host { get; set; }
    }

    public class RenewCertificatesOrchestration : ActionOrchestration
    {
        #region Fields
        protected EnterpriseArchitectClient entArch;

        protected EnterpriseManagerClient entMgr;
        #endregion

        #region Constructors
        public RenewCertificatesOrchestration(EnterpriseArchitectClient entArch, EnterpriseManagerClient entMgr)
        {
            this.entArch = entArch;

            this.entMgr = entMgr;
        }
        #endregion

        #region API Methods
        [FunctionName("RenewCertificatesOrchestration")]
        public virtual async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext ctx)
        {
            string entApiKey;

            try
            {
                var actionArgs = ctx.GetInput<ExecuteActionArguments>();

                entApiKey = actionArgs.StateDetails.EnterpriseAPIKey;
            }
            catch
            {
                entApiKey = ctx.GetInput<string>();
            }

            var hostsForRenewal = await ctx.CallActivityAsync<List<string>>("RetrieveHostsForRenewal", entApiKey);

            var hostEnvRenewalTasks = hostsForRenewal.Select(host =>
            {
                return ctx.CallActivityAsync<List<RenewalEnvironment>>("RetrieveRenewalEnvironments", host);
            });

            var hostEnvRenewalGroups = await Task.WhenAll(hostEnvRenewalTasks);

            var hostEnvRenewals = hostEnvRenewalGroups.Where(herg => !herg.IsNullOrEmpty()).ToDictionary(herg => herg.First().Host, herg => herg.ToList());

            if (!hostEnvRenewals.IsNullOrEmpty())
            {
                var retryOptions = new RetryOptions(
                    firstRetryInterval: TimeSpan.FromSeconds(30),
                    maxNumberOfAttempts: 3);

                var hostsSslCertTasks = hostEnvRenewals.Select(her =>
                {
                    return ctx.CallActivityWithRetryAsync<Status>("GenerateNewSSLCertificate", retryOptions, her.Value.First());
                });

                var hostSslCertsStati = await Task.WhenAll(hostsSslCertTasks);

                if (hostSslCertsStati.All(s => s))
                {
                    var renewTasks = hostEnvRenewals.SelectMany(herg =>
                    {
                        return herg.Value.Select(her =>
                        {
                            return ctx.CallActivityWithRetryAsync<Status>("RenewCertificatesForHostEnvironment", retryOptions, her);
                        });
                    });

                    var renewals = await Task.WhenAll(renewTasks);

                    var success = renewals.All(s => s);
                }
            }
        }

        [FunctionName("RetrieveHostsForRenewal")]
        public virtual async Task<List<string>> RetrieveHostsForRenewal([ActivityTrigger] string entApiKey, ILogger log)
        {
            var regHosts = await entMgr.ListRegistrationHosts(entApiKey);

            if (regHosts.Status)
            {
                var renewalHostTasks = regHosts.Model.Select(regHost =>
                {
                    return entMgr.FindRegisteredHosts(entApiKey, regHost);
                    // return entMgr.Get<BaseResponse<List<string>>>($"hosting/{entApiKey}/find-hosts/{regHost}");
                });

                var renewalHostResults = await Task.WhenAll(renewalHostTasks);

                var renewalHosts = renewalHostResults.SelectMany(rhr => rhr.Model ?? new List<string>()).ToList();

                return renewalHosts;
            }
            else
                return new List<string>();
        }

        [FunctionName("RetrieveRenewalEnvironments")]
        public virtual async Task<List<RenewalEnvironment>> RetrieveRenewalEnvironments([ActivityTrigger] string host, ILogger log)
        {
            // log.LogInformation($"Saying hello to {name}.");

            var renewalEnvs = new List<RenewalEnvironment>();

            //  Call a Persona API to retrieve any enterprise envirnments actually using the host... The persona api will use the graph to retrieve data, and Azure API to verify
            //      - For each host, lookup enterpriseApiKey, and then get list of environments for that API key
            //      - New Persona API endpoint to validate Azure Hosting is live;  call for each returned env
            //              - entMgr.HasValidHostedApp(entApiKey, envLookup)
            //                  
            //                  var infraCfg = await getAzureInfraConfig(prvGraph, entCtxt.EntApiKey, envLookup);

            //                  var creds = getAzureAuthorization(infraCfg);

            //                  var azure = getAzureHook(creds, infraCfg.AzureSubID);

            //                  var webApp = azure.AppServices.WebApps.GetByResourceGroup(envLookup, envLookup);

            //                  var enabledEnvs = envs.Where(env => webApp.EnabledHostNames.Contains(env)).ToList();

            //                  renewalEnvs.AddRange(enabledEnvs.Select(env => new RenewalEnvironment() {
            //                      EnterpriseAPIKey = entCtxt.EntApiKey,
            //                      EnvironmentLookup = envLookup,
            //                      Host = host
            //                  }));

            var entCtxt = await entMgr.ResolveHost(host, false);

            // var envs = await entMgr.ListEnvironments(entCtxt.EntApiKey);

            return renewalEnvs;
        }

        [FunctionName("GenerateNewSSLCertificate")]
        public virtual Status GenerateNewSSLCertificate([ActivityTrigger] RenewalEnvironment renewalEnv, ILogger log)
        {
            // log.LogInformation($"Saying hello to {name}.");

            //  Call a Persona API
            //      - Kevin has all of this logic in the console app, i don't think yet ported into persona APIs (check with Kevin)
            //          - The persona API should not return the cert, just continue storing in the blob
            //      - Create a * cert for all hosts in the return from g.V().HasLabel('EnterpriseRegistration').Has('EnterpriseAPIKey', entApiKey).Properties('Hosts').Value()

            // entArch = req.ResolveClient<EnterpriseArchitectClient>(logger);

            return Status.Success;
        }

        [FunctionName("RenewCertificatesForHostEnvironment")]
        public virtual Status RenewCertificatesForHostEnvironment([ActivityTrigger] RenewalEnvironment renewalEnv, ILogger log)
        {
            // log.LogInformation($"Saying hello to {name}.");

            //  Call a Persona API to deploy cert... Call
            //      - entArch > Hosting Controller > EnsureHostsSSL
            //      - 

            // entArch = req.ResolveClient<EnterpriseArchitectClient>(logger);

            //  entArch.EnsureHostsSSL(new EnsureHostsSSLRequest(){ Hosts = new List<string>() { renewalEnv.Host }}, renewalEnv.EnterpriseAPIKey,
            //      renewalEnv.EnvironmentLookup, parentEntApiKey: entApikEy);

            return Status.Success;
        }
        #endregion

    }
}