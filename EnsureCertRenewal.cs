using System;
using System.IO;
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

namespace LCU.State.API.NapkinIDE.InfrastructureManagement
{
    public class EnsureCertRenewal
    {
        #region API Methods
        [FunctionName("EnsureCertRenewalTimer")]
        public virtual async Task RunTimer([TimerTrigger("0 0 1 * * *", RunOnStartup = true)]TimerInfo myTimer,
            [DurableClient] IDurableOrchestrationClient starter, ILogger log)
        {
            log.LogInformation($"Ensuring Certificate Renewals via Timer: {DateTime.Now}");

            var entApiKey = Environment.GetEnvironmentVariable("LCU-ENTERPRISE-API-KEY");

            try
            {
                var instanceId = await starter.StartAction2("RenewCertificatesOrchestration", new StateDetails()
                {
                    EnterpriseAPIKey = entApiKey,
                    Host = "",
                    HubName = "",
                    StateKey = "",
                    Username = ""
                }, new ExecuteActionRequest()
                {
                    Arguments = new
                    {
                        EnterpriseAPIKey = entApiKey
                    }.JSONConvert<MetadataModel>()
                }, log);
            }
            catch (Exception ex)
            {

            }
        }

        [FunctionName("EnsureCertRenewal")]
        public virtual async Task<IActionResult> RunAPI([HttpTrigger]HttpRequest req, [DurableClient] IDurableOrchestrationClient starter, ILogger log,
            [SignalR(HubName = InfrastructureManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            log.LogInformation($"Ensuring Certificate Renewals via API");

            var entApiKey = Environment.GetEnvironmentVariable("LCU-ENTERPRISE-API-KEY");

            var instanceId = await starter.StartAction2("RenewCertificatesOrchestration", new StateDetails()
                {
                    EnterpriseAPIKey = entApiKey,
                    Host = "",
                    HubName = "",
                    StateKey = "",
                    Username = ""
                }, new ExecuteActionRequest()
                {
                    Arguments = new
                    {
                        EnterpriseAPIKey = entApiKey
                    }.JSONConvert<MetadataModel>()
                }, log);
                
			return starter.CreateCheckStatusResponse(req, instanceId);
        }
        #endregion
    }

    public static class Exts
    {
        public static async Task<IActionResult> StartAction2(this IDurableOrchestrationClient starter, string actionName, HttpRequest req, ILogger log, int terminateDelayMilliseconds = 5000)
        {
            var stateDetails = StateUtils.LoadStateDetails(req);

            var instanceId = await starter.StartAction2(actionName, stateDetails, await req.LoadBody<ExecuteActionRequest>(), log, terminateDelayMilliseconds: terminateDelayMilliseconds);

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        public static async Task<string> StartAction2(this IDurableOrchestrationClient starter, string actionName, StateDetails stateDetails, ExecuteActionRequest exActReq, ILogger log, int terminateDelayMilliseconds = 5000)
        {
            var instanceId = $"{stateDetails.EnterpriseAPIKey}-{stateDetails.HubName}-{stateDetails.Username}-{stateDetails.StateKey}";

            instanceId = await starter.StartAction2(actionName, stateDetails, exActReq, log, terminateDelayMilliseconds: terminateDelayMilliseconds, instanceId: instanceId);

            return instanceId;
        }

        public static async Task<string> StartAction2(this IDurableOrchestrationClient starter, string actionName, StateDetails stateDetails, ExecuteActionRequest exActReq, ILogger log, int terminateDelayMilliseconds,
            string instanceId = null)
        {
            if (!instanceId.IsNullOrEmpty())
            {
                var instanceStatus = await starter.GetStatusAsync(instanceId, false);

                if (instanceStatus != null && (instanceStatus.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew ||
                    instanceStatus.RuntimeStatus == OrchestrationRuntimeStatus.Pending || instanceStatus.RuntimeStatus == OrchestrationRuntimeStatus.Running))
                {
                    await starter.TerminateAsync(instanceId, $"Restarting action {actionName} with id {instanceId}");

                    await Task.Delay(terminateDelayMilliseconds);
                }
            }

            log.LogInformation($"Starting Action {actionName} with id {instanceId ?? ""}");

            instanceId = await starter.StartNewAsync(actionName, instanceId, new StateActionContext()
            {
                ActionRequest = exActReq,
                StateDetails = stateDetails
            });

            log.LogInformation($"Started orchestration with id {instanceId}.");

            return instanceId;
        }
    }
}
