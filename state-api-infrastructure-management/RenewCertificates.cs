using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fathym;
using LCU.Presentation.State.ReqRes;
using LCU.StateAPI;
using LCU.StateAPI.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Linq;

namespace LCU.State.API.NapkinIDE.InfrastructureManagement
{
    public class GenericRenewCertificates
    {
        #region Helpers
        public virtual async Task<string> runAction(IDurableOrchestrationClient starter, ILogger log)
        {
            var entApiKey = Environment.GetEnvironmentVariable("LCU-ENTERPRISE-API-KEY");

            try
            {
                var listInstanceQuery = new OrchestrationStatusQueryCondition()
                {
                    PageSize = 1000,
                    RuntimeStatus = new[] { OrchestrationRuntimeStatus.Running, OrchestrationRuntimeStatus.Pending }
                };

                var instances = await starter.ListInstancesAsync(listInstanceQuery, new System.Threading.CancellationToken());

                var running = new HashSet<string>(instances.DurableOrchestrationState.Select(instance => instance.InstanceId));

                var terminateTasks = running.Select(instanceId =>
                {
                    return starter.TerminateAsync(instanceId, "Cleanup");
                });

                await Task.WhenAll(terminateTasks);

                while (running.Count > 0)
                {
                    var results = await Task.WhenAll(instances.DurableOrchestrationState.Select(instance => starter.GetStatusAsync(instance.InstanceId)));

                    foreach (var status in results)
                    {
                        // Remove any terminated or completed instances from the hashset
                        if (status != null &&
                            status.RuntimeStatus != OrchestrationRuntimeStatus.Pending &&
                            status.RuntimeStatus != OrchestrationRuntimeStatus.Running)
                        {
                            running.Remove(status.InstanceId);
                        }
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(1000));
                }

                var instanceId = await starter.StartAction("RenewCertificatesOrchestration", new StateDetails()
                {
                    EnterpriseAPIKey = entApiKey
                }, new ExecuteActionRequest()
                {
                    Arguments = new
                    {
                        EnterpriseAPIKey = entApiKey
                    }.JSONConvert<MetadataModel>()
                }, log);

                return instanceId;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        #endregion
    }

    public class RenewCertificatesTimer : GenericRenewCertificates
    {
        #region API Methods
        [FunctionName("RenewCertificatesTimer")]
        public virtual async Task RunTimer([TimerTrigger("0 0 1 * * *", RunOnStartup = true)]TimerInfo myTimer,
            [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            log.LogInformation($"Ensuring Certificate Renewals via Timer: {DateTime.Now}");

            var instanceId = await runAction(starter, log);
        }
        #endregion
    }

    public class RenewCertificatesAPI : GenericRenewCertificates
    {
        #region API Methods
        [FunctionName("RenewCertificates")]
        public virtual async Task<IActionResult> RunAPI([HttpTrigger]HttpRequest req, [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            log.LogInformation($"Ensuring Certificate Renewals via API");

            var instanceId = await runAction(starter, log);

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
        #endregion
    }
}
