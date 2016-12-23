using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Badgibot
{
    [Serializable]
    public class MainMenuDialog : IDialog<IMessageActivity>
    {
        public MainMenuDialog(string baseUrl)
        {
            BaseUrl = baseUrl.Replace("api/messages", "Content/images/");
        }

        string BaseUrl { get; set; }

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(MainMenuAsync);
        }

        public async Task MainMenuAsync(IDialogContext context, IAwaitable<IMessageActivity> argument)
        {
            try
            {
                var msg = await argument;
                var cmd = Command.Parse(msg.Text);

                using (var db = new MessagesEntities())
                {
                    db.Database.Log = s => Debug.WriteLine(s);

                    IQueryable<Message> rows = db.Messages;
                    if (cmd.Author != null)
                        rows = rows.Where(m => m.Author == cmd.Author);
                    if (cmd.HasDateFilter)
                        rows = rows.Where(m => m.SentAt >= cmd.StartTime && m.SentAt <= cmd.EndTime);
                    if (cmd.HasType)
                        rows = rows.Where(m => m.Type == cmd.Type);
                    if (cmd.HasId)
                        rows = rows.Where(m => m.MessagesId == cmd.Id);

                    var randomRow = rows.OrderBy(r => Guid.NewGuid()).Take(1).SingleOrDefault();
                    if (randomRow == null)
                    {
                        await context.MakeAndSendReply("Sorry, I couldn't find any messages meeting that criteria.");
                        context.Wait(MainMenuAsync);
                    }

                    var startId = randomRow.MessagesId - cmd.ContextLines;
                    var finalRows = db.Messages.Where(m => m.MessagesId >= startId)
                                               .Take(cmd.ContextLines * 2 + 1);

                    foreach (var r in finalRows)
                    {
                        var reply = context.MakeMessage();
                        reply.Text = $"[{r.SentAt.ToString("M/d/yy h:mmtt").ToLower()}] {r.Author}:\n\n";

                        switch (r.Type)
                        {
                            case MessageType.Text:
                                reply.Text += r.Content;
                                break;
                            case MessageType.Image:
                            case MessageType.Audio:
                            case MessageType.Video:
                                reply.Attachments = new[]
                                {
                                    new Attachment
                                    {
                                        ContentType = r.ContentType,
                                        ContentUrl = BaseUrl + r.Content,
                                    }
                                };
                                break;
                        }
                        await context.PostAsync(reply);
                    }
                }
            }
            catch (Exception e)
            {
                await context.MakeAndSendReply(e.ToString());
            }
            context.Wait(MainMenuAsync);
        }
    }

    static class HandyExtensions
    {
        public static Task MakeAndSendReply(this IDialogContext context, string text)
        {
            var reply = context.MakeMessage();
            reply.Text = text;
            return context.PostAsync(reply);
        }
    }
}
