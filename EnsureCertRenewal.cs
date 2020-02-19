using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace LCU.State.API.NapkinIDE.Infrastructure.Management
{
    public static class EnsureCertRenewal
    {
        [FunctionName("EnsureCertRenewal")]
        public static async Task Run([TimerTrigger("* * * */1 * *", RunOnStartup = true)]TimerInfo myTimer,
            [DurableClient] IDurableOrchestrationClient actions, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            var entApiKey = Environment.GetEnvironmentVariable("LCU-ENTERPRISE-API-KEY");

            var startCertRenewal = false;

            var renewStatus = await actions.GetStatusAsync(entApiKey);

            startCertRenewal = renewStatus == null;  // TODO:  Determine if something else has to be done

            if (startCertRenewal)
            {
                await actions.StartNewAsync("RenewCertificates", entApiKey, entApiKey);
            }
            else
            {
                //  Anything we need to do if it is running?  check its state? If in a bad state end Orchestration
            }
        }
    }
}
