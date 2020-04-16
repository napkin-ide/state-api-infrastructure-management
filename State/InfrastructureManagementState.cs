using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using LCU.StateAPI.Utilities;
using LCU.Personas.Client.Enterprises;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using LCU.Presentation.State.ReqRes;
using System.Runtime.Serialization;

namespace LCU.State.API.NapkinIDE.InfrastructureManagement.State
{
    [Serializable]
    [DataContract]
    public class InfrastructureManagementState
    {
        #region Constants
        public const string HUB_NAME = "infrastructuremanagement";
        #endregion
        
        [DataMember]
        public virtual string FathymDashboardURL { get; set; }
    }
}
