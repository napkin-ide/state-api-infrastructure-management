using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Fathym;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Runtime.Serialization;
using Fathym.API;
using System.Collections.Generic;
using System.Linq;
using LCU.Personas.Client.Applications;
using LCU.StateAPI.Utilities;
using System.Security.Claims;
using LCU.Personas.Client.Enterprises;
using LCU.Personas.Client.Security;
using LCU.State.API.NapkinIDE.InfrastructureManagement.State;

namespace LCU.State.API.NapkinIDE.InfrastructureManagement
{
    [Serializable]
    [DataContract]
    public class RefreshRequest : BaseRequest
    { }

    public class Refresh
    {
        #region Fields
        protected SecurityManagerClient secMgr;
        #endregion

        #region Constructors
        public Refresh(SecurityManagerClient secMgr)
        {
            this.secMgr = secMgr;
        }
        #endregion

        [FunctionName("Refresh")]
        public virtual async Task<Status> Run([HttpTrigger] HttpRequest req, ILogger log,
            [SignalR(HubName = InfrastructureManagementState.HUB_NAME)]IAsyncCollector<SignalRMessage> signalRMessages,
            [Blob("state-api/{headers.lcu-ent-api-key}/{headers.lcu-hub-name}/{headers.x-ms-client-principal-id}/{headers.lcu-state-key}", FileAccess.ReadWrite)] CloudBlockBlob stateBlob)
        {
            return await stateBlob.WithStateHarness<InfrastructureManagementState, RefreshRequest, InfrastructureManagementStateHarness>(req, signalRMessages, log,
                async (harness, refreshReq, actReq) =>
            {
                log.LogInformation($"Refresh");

                var stateDetails = StateUtils.LoadStateDetails(req);

                await harness.GetFathymDashboardURL(secMgr, stateDetails.EnterpriseAPIKey);

                return Status.Success;
            });
        }
    }
}
