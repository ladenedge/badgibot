using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Badgibot;
using HtmlAgilityPack;

namespace SmsImport
{
    class Program
    {
        const string BadgibotDirectory = @"..\..\..\Badgibot";
        const string ExportDirectory = @"c:\Temp\Badger\Badgibot\iUnicorn iPhone SE\Messages\2016-12-21\SMS\Heather Herrell";

        static void Main(string[] args)
        {
            foreach (var file in Directory.EnumerateFiles(ExportDirectory))
                ImportMessageFile(file);
            //CopyAttachments();
        }

        private static void ImportMessageFile(string file)
        {
            Console.WriteLine($"Reading file {Path.GetFileName(file)}...");

            var html = new HtmlDocument();
            html.Load(file, Encoding.UTF8);
            html.OptionOutputAsXml = true;
            var htmlStream = new MemoryStream();
            html.Save(htmlStream);
            htmlStream.Position = 0;

            var xmlStream = new MemoryStream();
            using (var sw = new StreamWriter(xmlStream, Encoding.UTF8))
            {
                TranslateEmoticons(sw, htmlStream);
                xmlStream.Position = 0;

                var doc = XDocument.Load(xmlStream);
                var all = from li in doc.Descendants("li")
                          where li.Attribute("class").Value == "date-time"
                          select new
                          {
                              Day = li.Element("p").Value,
                              Msgs = from perAuthor in MessagesByDay(li)
                                     from msg in perAuthor.Descendants("li")
                                     where msg.Attribute("class").Value.Contains("message")
                                     select new
                                     {
                                         Message = msg,
                                         Time = msg.Element("span").Value,
                                         Username = (from attrs in perAuthor.Descendants("li")
                                                     where attrs.Attribute("class").Value == "user-name"
                                                     select attrs.Element("p").Value).SingleOrDefault(),
                                     },
                          };

                var messages = new List<Message>();
                foreach (var messagesByDay in all)
                {
                    var day = DateTime.Parse(messagesByDay.Day);
                    foreach (var rawMsg in messagesByDay.Msgs)
                    {
                        var time = DateTime.Parse(rawMsg.Time);
                        var msg = new Message
                        {
                            SentAt = day.AddTicks(time.TimeOfDay.Ticks),
                            Author = TranslateUsername(rawMsg.Username),
                        };

                        var css = rawMsg.Message.Attribute("class").Value;
                        if (css.Contains("image"))
                        {
                            msg.Type = MessageType.Image;
                            msg.Content = rawMsg.Message.Element("a").Attribute("href").Value;
                        }
                        else if (css.Contains("video"))
                        {
                            msg.Type = MessageType.Video;
                            msg.Content = rawMsg.Message.Element("a").Attribute("href").Value;
                        }
                        else if (css.Contains("file"))
                        {
                            var content = rawMsg.Message.Element("a").Attribute("href").Value;
                            if (content.Contains("amr"))
                            {
                                msg.Type = MessageType.Audio;
                                msg.Content = content.Replace(".amr", ".mp3");
                            }
                            else
                            {
                                msg.Type = MessageType.Image;
                                msg.Content = content.Replace(".pluginPayloadAttachment", ".png");
                            }
                        }
                        else if (css.Contains("contacts"))
                        {
                            continue;
                        }
                        else if (css == "message")
                        {
                            msg.Type = MessageType.Text;
                            msg.Content = DecodeTextContent(rawMsg.Message.Element("p").Value);
                        }
                        else
                            throw new InvalidOperationException($"Unexpected class '{css}'");

                        msg.Content = FixAttachmentReferences(msg.Content);
                        messages.Add(msg);
                    }
                }

                SaveMessages(messages);
            }
        }

        private static void SaveMessages(List<Message> messages)
        {
            Console.WriteLine("Saving to database...");
            using (var db = new MessagesEntities())
            {
                db.Messages.AddRange(messages);
                db.SaveChanges();
            }
        }

        private static void TranslateEmoticons(StreamWriter sw, Stream htmlStream)
        {
            string line;
            using (var sr = new StreamReader(htmlStream, Encoding.UTF8))
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains("emoticon"))
                    {
                        line = Regex.Replace(line, @"<img .*? alt=""([^""]*)"" .*?""message_emoticon.*?"" />", "$1");
                        line = line.Trim();
                    }
                    sw.WriteLine(line);
                }
            sw.Flush();
        }

        static string TranslateUsername(string username)
        {
            if (username.StartsWith("Heather"))
                return "Badger";
            if (username.StartsWith("Jay"))
                return "Unicorn";
            throw new ArgumentException($"Unknown user '{username}'");
        }

        static string DecodeTextContent(string content)
        {
            if (content.Any(c => Char.IsControl(c)))
                content = content.Replace("\n", "");
            content = WebUtility.HtmlDecode(content);
            return content;
        }

        static string FixAttachmentReferences(string location)
        {
            return Regex.Replace(location, @"Heather Herrell\[(\d+)\]", "$1");
        }

        static IEnumerable<XElement> MessagesByDay(XElement li)
        {
            var messageNodes = new List<XElement>();
            foreach (var element in li.ElementsAfterSelf())
            {
                var css = element.Attribute("class");
                if (element.Name != "li" || css == null || css.Value == "date-time")
                    break;
                if (css.Value == "clear")
                    continue;
                messageNodes.Add(element);
            }
            return messageNodes;
        }

        private static void CopyAttachments()
        {
            Console.WriteLine("Copying attachments...");

            var imgDir = Directory.CreateDirectory(Path.Combine(BadgibotDirectory, @"Content\images"));

            foreach (var dir in Directory.EnumerateDirectories(ExportDirectory))
            {
                var counterMatch = Regex.Match(dir, @"\[(\d+)\]");
                if (!counterMatch.Success)
                    throw new DirectoryNotFoundException($"Unexpected attachment directory name: {dir}");
                var destDir = Directory.CreateDirectory(Path.Combine(imgDir.FullName, counterMatch.Groups[1].Value));

                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    var dest = Path.Combine(destDir.FullName, Path.GetFileName(file));
                    var ext = Path.GetExtension(file).Trim('.');

                    if (AttachmentHandlers.ContainsKey(ext))
                        AttachmentHandlers[ext](file, dest);
                    else
                        File.Copy(file, dest, true);
                }
            }
        }

        static IDictionary<string, Action<string, string>> AttachmentHandlers = new Dictionary<string, Action<string, string>>
        {
            { "pluginPayloadAttachment", (s, d) => File.Copy(s, d.Replace("pluginPayloadAttachment", "png"), true) },
            { "amr", (s, d) => ConvertAudio(s, d.Replace("amr", "mp3")) },
        };

        private static void ConvertAudio(string src, string dest)
        {
            Process process = new Process();
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.FileName = "ffmpeg.exe";
            process.StartInfo.Arguments = $"-i \"{src}\" -ar 22050 \"{dest}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
        }
    }
}
