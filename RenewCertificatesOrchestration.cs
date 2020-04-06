using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Fathym;
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

    public class RenewCertificatesOrchestration
    {
        [FunctionName("RenewCertificatesOrchestration")]
        public async Task RunOrchestrator(
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

            var hostEnvRenewals = hostEnvRenewalGroups.SelectMany(grp => grp);

            if (!hostEnvRenewals.IsNullOrEmpty())
            {
                var retryOptions = new RetryOptions(
                    firstRetryInterval: TimeSpan.FromSeconds(5),
                    maxNumberOfAttempts: 3);

                var certGenerateStatus = await ctx.CallActivityWithRetryAsync<Status>("GenerateNewSSLCertificate", retryOptions,
                    entApiKey);

                if (certGenerateStatus)
                {
                    var renewTasks = hostEnvRenewals.Select(her => ctx.CallActivityWithRetryAsync<Status>("RenewCertificatesForHostEnvironment",
                        retryOptions, new Tuple<string, RenewalEnvironment>(entApiKey, her)));

                    var renewals = await Task.WhenAll(renewTasks);
                }
            }
        }

        [FunctionName("RetrieveHostsForRenewal")]
        public static List<string> RetrieveHostsForRenewal([ActivityTrigger] string entApiKey, ILogger log)
        {
            // log.LogInformation($"Saying hello to {name}.");

            //  Call a Persona API to get a list of domains that are in need of renewal... The persona api will use the graph
            //      - EnterpriseRegistration (assume always one) - Lookup by entApiKey and return list of enterprise hosts (g.V().HasLabel('EnterpriseRegistration').Has('EnterpriseAPIKey', entApiKey).Properties('Hosts').Value())
            //      - Take list of hosts and lookup all enterprises that leverage a *.{entHost} host 
            //              - Run g.V().HasLabel('Enterprise').Properties('Hosts').Value() and pull out all of them that end with any host returned from EnterpriseRegistration

            // entArch = req.ResolveClient<EnterpriseArchitectClient>(logger);

            return new List<string>() {
                "www.fathym-it.com",
                "mike-prd-test1.fathym-it.com",
                "mike-prd-test2.fathym-it.com",
                "____.fathym-it.com"
            };
        }

        [FunctionName("RetrieveRenewalEnvironments")]
        public static List<RenewalEnvironment> RetrieveRenewalEnvironments([ActivityTrigger] string host, ILogger log)
        {
            // log.LogInformation($"Saying hello to {name}.");

            var renewalEnvs = new List<RenewalEnvironment>();

            //  Call a Persona API to retrieve any enterprise envirnments actually using the host... The persona api will use the graph to retrieve data, and Azure API to verify
            //      - For each host, lookup enterpriseApiKey, and then get list of environments for that API key
            //              - var entCtxt = await entMgr.ResolveHost(host);
            //              - var envs = await entMgr.ListEnvironments(entCtxt.EntApiKey);
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

            return renewalEnvs;
        }

        [FunctionName("GenerateNewSSLCertificate")]
        public static Status GenerateNewSSLCertificate([ActivityTrigger] string entApiKey, ILogger log)
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
        public static Status RenewCertificatesForHostEnvironment([ActivityTrigger] Tuple<string, RenewalEnvironment> renewalEnvCfg, ILogger log)
        {
            // log.LogInformation($"Saying hello to {name}.");

            var entApiKey = renewalEnvCfg.Item1;

            var renewalEnv = renewalEnvCfg.Item2;

            //  Call a Persona API to deploy cert... Call
            //      - entArch > Hosting Controller > EnsureHostsSSL
            //      - 

            // entArch = req.ResolveClient<EnterpriseArchitectClient>(logger);

            //  entArch.EnsureHostsSSL(new EnsureHostsSSLRequest(){ Hosts = new List<string>() { renewalEnv.Host }}, renewalEnv.EnterpriseAPIKey,
            //      renewalEnv.EnvironmentLookup, parentEntApiKey: entApikEy);

            return Status.Success;
        }
    }
}