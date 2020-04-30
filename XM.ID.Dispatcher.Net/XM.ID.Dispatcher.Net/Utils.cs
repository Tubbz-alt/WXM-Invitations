﻿using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace XM.ID.Dispatcher.Net
{
    public static class Utils
    {
        #region Logging-Related
        /// <summary>
        /// Creates a Log-Event to capture application-level + invitation-level logs
        /// </summary>
        /// <param name="azureQueueData"></param>
        /// <param name="logMessage"></param>
        /// <returns></returns>
        public static LogEvent CreateLogEvent(AzureQueueData azureQueueData, LogMessage logMessage)
        {
            var UtcNow = DateTime.UtcNow;
            return new LogEvent
            {
                BatchId = azureQueueData?.BatchId,
                Created = UtcNow,
                DeliveryWorkFlowId = null,
                DispatchId = azureQueueData?.DispatchId,
                Events = null,
                Id = ObjectId.GenerateNewId().ToString(),
                Location = null,
                LogMessage = logMessage,
                Prefills = azureQueueData?.MappedValue?.ToList()?.Select(x => new Prefill { QuestionId = x.Key, Input = null, Input_Hash = x.Value })?.ToList(),
                Tags = new List<string> { "Dispatcher" },
                Target = null,
                TargetHashed = azureQueueData?.CommonIdentifier,
                TokenId = azureQueueData?.TokenId,
                Updated = UtcNow,
                User = azureQueueData?.User
            };
        }

        /// <summary>
        /// Create an Invitation-Log-Event to capture a unique stage in an Invitation's Journey
        /// </summary>
        /// <param name="eventAction"></param>
        /// <param name="eventChannel"></param>
        /// <param name="azureQueueData"></param>
        /// <param name="logMessage"></param>
        /// <returns></returns>
        public static InvitationLogEvent CreateInvitationLogEvent(EventAction eventAction, EventChannel eventChannel, AzureQueueData azureQueueData, LogMessage logMessage)
        {
            return new InvitationLogEvent
            {
                Action = eventAction,
                Channel = eventChannel,
                EventStatus = null,
                LogMessage = logMessage,
                Message = $"Message Template Id: {azureQueueData?.TemplateId} | Additional Token Parameters: {azureQueueData.AdditionalURLParameter}",
                TargetId = eventChannel==EventChannel.Email ? azureQueueData?.EmailId : 
                eventChannel == EventChannel.SMS ? azureQueueData?.MobileNumber : eventChannel.ToString(),
                TimeStamp = DateTime.UtcNow
            };
        }

        internal static bool IsLogInsertible(LogMessage logMessage)
        {
            int level;
            if (logMessage == null)
                return true;
            else if (logMessage.Level == LogMessage.SeverityLevel_Critical)
                level = 1;
            else if (logMessage.Level == LogMessage.SeverityLevel_Error)
                level = 2;
            else if (logMessage.Level == LogMessage.SeverityLevel_Warning)
                level = 3;
            else if (logMessage.Level == LogMessage.SeverityLevel_Information)
                level = 4;
            else if (logMessage.Level == LogMessage.SeverityLevel_Verbose)
                level = 5;
            else
                level = 10;
            return level <= Resources.GetInstance().LogLevel;
        }

        internal static async Task FlushLogs(List<MessagePayload> messagePayloads)
        {
            try
            {
                List<LogEvent> logEvents = new List<LogEvent>();
                messagePayloads.ForEach(x => logEvents.AddRange(x.LogEvents.Where(y => IsLogInsertible(y.LogMessage))));
                if (logEvents.Count > 0)
                    await Resources.GetInstance().LogEventCollection.InsertManyAsync(logEvents);

                var filterBuilder = Builders<LogEvent>.Filter;
                var updateBuilder = Builders<LogEvent>.Update;
                var writeBuilder = new List<WriteModel<LogEvent>>();
                foreach (MessagePayload messagePayload in messagePayloads)
                {
                    if (messagePayload.InvitationLogEvents.Count > 0)
                    {
                        var filter = filterBuilder.Eq(x => x.Id, messagePayload.Invitation.Id);
                        var update = updateBuilder.PushEach(x => x.Events, messagePayload.InvitationLogEvents).Set(x => x.Updated, DateTime.UtcNow);
                        writeBuilder.Add(new UpdateOneModel<LogEvent>(filter, update));
                    }
                }
                if (writeBuilder.Count > 0)
                    await Resources.GetInstance().LogEventCollection.BulkWriteAsync(writeBuilder);
            }
            catch(Exception ex)
            {
                await FlushLogs(new List<LogEvent> { CreateLogEvent(null, IRDLM.InternalException(ex)) });
            }
            
        }

        internal static async Task FlushLogs(List<LogEvent> logEvents)
        {
            await Resources.GetInstance().LogEventCollection.InsertManyAsync(logEvents.Where(x => IsLogInsertible(x.LogMessage)));
        }
        #endregion

        #region BulkMessagePayloadManagement
        internal static async Task InsertBulkMessagePayload(MessagePayload messagePayload)
        {
            try
            {
                DB_MessagePayload dB_MessagePayload = new DB_MessagePayload(messagePayload);
                await Resources.GetInstance().BulkMessagePayloadCollection.InsertOneAsync(dB_MessagePayload);
            }
            catch (Exception ex)
            {
                await FlushLogs(new List<LogEvent> { CreateLogEvent(null, IRDLM.InternalException(ex)) });
            }
        }

        internal static async Task<List<DB_MessagePayload>> ReadBulkMessagePayloads()
        {
            try
            {
                return await Resources.GetInstance().BulkMessagePayloadCollection
                .Find(x => x.BulkVendorName == Resources.GetInstance().BulkVendorName.ToLower() && x.Status == "Ready")
                .Limit(Resources.GetInstance().BulkReadSize)
                .ToListAsync();
            }
            catch (Exception ex)
            {
                await FlushLogs(new List<LogEvent> { CreateLogEvent(null, IRDLM.InternalException(ex)) });
                return new List<DB_MessagePayload>();
            }
        }

        internal static async Task UpdateBulkMessagePayloads(List<DB_MessagePayload> dB_MessagePayloads)
        {
            try
            {
                List<string> docIds = dB_MessagePayloads.Select(x => x.Id).ToList();
                var filter = Builders<DB_MessagePayload>.Filter.In(x => x.Id, docIds);
                var update = Builders<DB_MessagePayload>.Update.Set(x => x.Status, "Processing");
                await Resources.GetInstance().BulkMessagePayloadCollection.UpdateManyAsync(filter, update);
            }
            catch (Exception ex)
            {
                await FlushLogs(new List<LogEvent> { CreateLogEvent(null, IRDLM.InternalException(ex)) });
            }
        }

        internal static async Task DeleteBulkMessagePayloads(List<DB_MessagePayload> dB_MessagePayloads)
        {
            try
            {
                List<string> docIds = dB_MessagePayloads.Select(x => x.Id).ToList();
                await Resources.GetInstance().BulkMessagePayloadCollection.DeleteManyAsync(x => docIds.Contains(x.Id));
            }
            catch (Exception ex)
            {
                await FlushLogs(new List<LogEvent> { CreateLogEvent(null, IRDLM.InternalException(ex)) });
            }
        }
        #endregion

        #region Hash-Look-Up
        /// <summary>
        /// Performs Hash-Look-Ups for PII Data if the received 
        /// AzureQueueData-Object's Subject/Text-Body/Html-Body
        /// utilizes WXM Tag-Substitution.
        /// </summary>
        /// <param name="azureQueueData"></param>
        public static void PerformLookUps(AzureQueueData azureQueueData)
        {
            try
            {
                string matchString = @"\$(\w+)\*\|(.*?)\|\*";

                //Subject
                if (!string.IsNullOrWhiteSpace(azureQueueData.Subject))
                {
                    StringBuilder newSubject = new StringBuilder(azureQueueData.Subject);
                    foreach (Match m in Regex.Matches(azureQueueData.Subject, matchString, RegexOptions.Multiline))
                    {
                        string qid = m.Groups[1].Value;
                        string defaultValue = m.Groups[2].Value;
                        if (azureQueueData.MappedValue.ContainsKey(qid))
                            newSubject.Replace(m.Groups[0].Value, azureQueueData.MappedValue[qid]);
                        else
                            newSubject.Replace(m.Groups[0].Value, defaultValue);
                    }
                    azureQueueData.Subject = newSubject.ToString();
                }

                //HTML Body
                if (!string.IsNullOrWhiteSpace(azureQueueData.HTMLBody))
                {
                    StringBuilder newHtmlBody = new StringBuilder(azureQueueData.HTMLBody);
                    foreach (Match m in Regex.Matches(azureQueueData.HTMLBody, matchString, RegexOptions.Multiline))
                    {
                        string qid = m.Groups[1].Value;
                        string defaultValue = m.Groups[2].Value;
                        if (azureQueueData.MappedValue.ContainsKey(qid))
                            newHtmlBody.Replace(m.Groups[0].Value, azureQueueData.MappedValue[qid]);
                        else
                            newHtmlBody.Replace(m.Groups[0].Value, defaultValue);
                    }
                    azureQueueData.HTMLBody = newHtmlBody.ToString();
                }

                //Text Body
                if (!string.IsNullOrWhiteSpace(azureQueueData.TextBody))
                {
                    StringBuilder newTextBody = new StringBuilder(azureQueueData.TextBody);
                    foreach (Match m in Regex.Matches(azureQueueData.TextBody, matchString, RegexOptions.Multiline))
                    {
                        string qid = m.Groups[1].Value;
                        string defaultValue = m.Groups[2].Value;
                        if (azureQueueData.MappedValue.ContainsKey(qid))
                            newTextBody.Replace(m.Groups[0].Value, azureQueueData.MappedValue[qid]);
                        else
                            newTextBody.Replace(m.Groups[0].Value, defaultValue);
                    }
                    azureQueueData.TextBody = newTextBody.ToString();
                }

            }
            catch (Exception ex)
            {
                FlushLogs(new List<LogEvent> { CreateLogEvent(null, IRDLM.InternalException(ex)) }).GetAwaiter().GetResult();
            }
        }
        #endregion

        #region Misc
        /// <summary>
        /// Generate a Survey-URL using the Token-Number & Additional-Parameters of the queue message and the configured Survey-Base-Domain
        /// </summary>
        /// <param name="azureQueueData"></param>
        /// <returns>Token-Number & Additional-Parameters specific Survey-URL</returns>
        public static string GetSurveyURL(AzureQueueData azureQueueData)
        {
            return $"http://{Resources.GetInstance().SurveyBaseDomain}/{azureQueueData.TokenId}{azureQueueData.AdditionalURLParameter}";
        }

        /// <summary>
        /// Generate an Unsubscribe-URL using the Token-Number of the queue message
        /// </summary>
        /// <param name="azureQueueData"></param>
        /// <returns>Token-Number specific Unsubscribe-URL</returns>
        public static string GetUnsubscribeURL(AzureQueueData azureQueueData)
        {
            return $"{Resources.GetInstance().UnsubscribeBaseUrl}{azureQueueData.TokenId}";
        }
        #endregion
    }
}