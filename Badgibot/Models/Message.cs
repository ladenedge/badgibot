using System.IO;

namespace Badgibot
{
    partial class Message
    {
        public string BoldAuthor
        {
            get
            {
                if (Author == "Unicorn")
                    return "𝗨𝗻𝗶𝗰𝗼𝗿𝗻";
                else if (Author == "Badger")
                    return "𝗕𝗮𝗱𝗴𝗲𝗿";
                else
                    return Author;
            }
        }

        public string ContentType
        {
            get
            {
                switch (Type)
                {
                    case MessageType.Text:
                        return "text/plain";
                    case MessageType.Image:
                        return "image/" + Path.GetExtension(Content).Trim('.').ToLower();
                    case MessageType.Audio:
                        return "audio/mpeg";
                    case MessageType.Video:
                        return "video/quicktime";
                    default:
                        return "text/plain";
                }
            }
        }

        public override string ToString()
        {
            return $"[{SentAt}] {Author}: {Content.Substring(0, 15)}";
        }
    }
}
