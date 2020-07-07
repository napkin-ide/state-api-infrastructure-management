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
using LCU.Personas.Enterprises;
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
                    return ctx.CallActivityWithRetryAsync<Status>("EnsureSSLCertificate", retryOptions, her.Value.First());
                });

                var hostSslCertsStati = await Task.WhenAll(hostsSslCertTasks);

                if (hostSslCertsStati.All(s => s))
                {
                    var renewTasks = hostEnvRenewals.SelectMany(herg =>
                    {
                        return herg.Value.Select(her =>
                        {
                            return ctx.CallActivityWithRetryAsync<Status>("RenewCertificatesForHostEnvironment", retryOptions, new Dictionary<string, RenewalEnvironment>()
                            {
                                { entApiKey, her }
                            });
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
            log.LogInformation($"RetrieveHostsForRenewal executing for enterprise: {entApiKey}");

            var regHosts = await entMgr.ListRegistrationHosts(entApiKey);

            if (regHosts.Status)
            {
                var renewalHostTasks = regHosts.Model.Select(regHost =>
                {
                    return entMgr.FindRegisteredHosts(entApiKey, regHost);
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
            log.LogInformation($"RetrieveRenewalEnvironments executing for: {host}");

            var renewalEnvs = new List<RenewalEnvironment>();

            var entResp = await entMgr.ResolveHost(host, false);

            if (entResp.Status)
            {
                var renewEnvResp = await entMgr.RetrieveRenewalEnvironments(host);

                if (renewEnvResp.Status)
                    renewalEnvs.AddRange(renewEnvResp.Model.Select(env => new RenewalEnvironment()
                    {
                        EnterpriseAPIKey = entResp.Model.PrimaryAPIKey,
                        EnvironmentLookup = env.Lookup,
                        Host = host
                    }));
            }

            return renewalEnvs;
        }

        [FunctionName("EnsureSSLCertificate")]
        public virtual async Task<Status> EnsureSSLCertificate([ActivityTrigger] RenewalEnvironment renewalEnv, ILogger log)
        {
            log.LogInformation($"EnsureSSLCertificate executing for: {renewalEnv.ToJSON()}");

            // var ensureCerts = await entArch.EnsureCertificates(new EnsureCertificatesRequest()
            // {
            //     Host = renewalEnv.Host
            // }, renewalEnv.EnterpriseAPIKey, renewalEnv.EnvironmentLookup);

            var ensureCerts = await entArch.Post<EnsureCertificatesRequest, BaseResponse>($"hosting/{renewalEnv.EnterpriseAPIKey}/ensure/certs/{renewalEnv.EnvironmentLookup}?parentEntApiKey={renewalEnv.EnterpriseAPIKey}", new EnsureCertificatesRequest()
            {
                Host = renewalEnv.Host
            });

            return ensureCerts.Status;
        }

        [FunctionName("RenewCertificatesForHostEnvironment")]
        public virtual async Task<Status> RenewCertificatesForHostEnvironment([ActivityTrigger] Dictionary<string, RenewalEnvironment> renewalEnvs, ILogger log)
        {
            log.LogInformation($"RenewCertificatesForHostEnvironment executing for: {renewalEnvs.ToJSON()}");

            var status = Status.Initialized;

            await renewalEnvs.Each(async renewalEnv =>
            {
                var ensureCerts = await entArch.EnsureHostsSSL(new EnsureHostsSSLRequest()
                {
                    Hosts = new List<string>() { renewalEnv.Value.Host }
                }, renewalEnv.Value.EnterpriseAPIKey, renewalEnv.Value.EnvironmentLookup, parentEntApiKey: renewalEnv.Key);

                status = ensureCerts.Status;

                return !status;
            });


            return status;
        }
        #endregion

    }
}