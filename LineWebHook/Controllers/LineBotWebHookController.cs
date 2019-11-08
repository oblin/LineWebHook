using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LineWebHook.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LineBotWebHookController : ControllerBase
    {
        const string token = "66seSTjHyMgSXpvYjQv0JSwec+JplIPrbOKGxvSBYd1FOzIv6wZNYJjqkeD/VvZkLQTjB9V6pBzT1RWfUKphrnPM3f1Q/tMOfDBV5dxQEydcCaK1WELa1sq5cn0fkEvJ7aP2aeVsE2+9/qFR++c3vQdB04t89/1O/w1cDnyilFU=";
        const string adminId = "U0096373bcd4f49c93098761794303f96";
        const string channelSecret = "8ebbcc238481fe83930f53e1637b8197";

        private readonly IHostingEnvironment _hostingEnvironment;

        public LineBotWebHookController(IHostingEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
        }

        [HttpGet]
        public ActionResult Get()
        {
            return Ok("Get OK");
        }

        [HttpPost("simple")]
        public ActionResult Simple()
        {
            
            try
            {
                string json;
                using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
                {
                    json = reader.ReadToEndAsync().Result;
                }
                // Verify Channel Secret
                var signature = Request.Headers["X-Line-Signature"];
                var hashvalue = GetHash(json, channelSecret);
                if (signature != hashvalue)
                {
                    var adminbot = new isRock.LineBot.Bot(token);
                    adminbot.PushMessage(adminId, $"Signature is wrong: {signature}, {hashvalue}");
                    return Ok();
                }

                var receivedMessage = isRock.LineBot.Utility.Parsing(json);
                
                var bot = new isRock.LineBot.Bot(token);

                var eventObject = receivedMessage.events[0];
                string message = string.Empty;

                switch(eventObject.type.ToLower())
                {
                    case "message":
                        var eventMessage = receivedMessage.events[0].message;
                        if (string.Equals(eventMessage.text, "bye", StringComparison.OrdinalIgnoreCase))
                        {
                            if (eventObject.source.type.ToLower() == "room")
                            {
                                isRock.LineBot.Utility.LeaveRoom(eventObject.source.roomId, token);
                            }
                            if (eventObject.source.type.ToLower() == "group")
                            {
                                isRock.LineBot.Utility.LeaveGroup(eventObject.source.groupId, token);
                            }
                            bot.PushMessage(adminId, "Bot had been kick of room/group");
                        }
                        else
                        {
                            isRock.LineBot.LineUserInfo userInfo = null;
                            if (eventObject.source.type.ToLower() == "room")
                            {
                                userInfo = isRock.LineBot.Utility.GetRoomMemberProfile(eventObject.source.roomId, eventObject.source.userId, token);
                            }
                            else if (eventObject.source.type.ToLower() == "group")
                            {
                                userInfo = isRock.LineBot.Utility.GetGroupMemberProfile(eventObject.source.groupId, eventObject.source.userId, token);
                            }
                            else
                            {
                                userInfo = bot.GetUserInfo(eventObject.source.userId);
                            }
                            message = ProcessMessageEvent(eventMessage, userInfo);
                            // 回覆用戶
                            bot.ReplyMessage(eventObject.replyToken, message);
                        }
                        break;
                    case "follow":
                        message = "Line bot 被加入好友";
                        bot.PushMessage(adminId, message);
                        break;
                    case "unfollow":
                        message = "Line bot 被封鎖";
                        bot.PushMessage(adminId, message);
                        break;
                    case "join":
                        var sourceId = eventObject.source.type.ToLower() == "room" ? eventObject.source.roomId : eventObject.source.groupId;
                        message = $"Line bot 被加入 {eventObject.source.type} 中, id: {sourceId}";
                        bot.ReplyMessage(eventObject.replyToken, message);
                        bot.PushMessage(adminId, message);
                        break;
                    case "leave":
                        message = "Line bot 離開聊天室";
                        bot.PushMessage(adminId, message);
                        break;
                    case "postback":
                        // 抓取 postback 的 data 屬性
                        var postdata = eventObject.postback.data;
                        // parsing data
                        var data = System.Web.HttpUtility.ParseQueryString(postdata);
                        message = "收到 postback:\n";
                        foreach(var item in data.AllKeys)
                        {
                            message += $" key: {item}, value: {data[item]}";
                        }
                        bot.ReplyMessage(eventObject.replyToken, message);
                        break;
                }

                return Ok();
            }
            catch (Exception ex)
            {
                // 如果有錯誤， PUSH 給 ADMIN
                var bot = new isRock.LineBot.Bot(token);
                bot.PushMessage(adminId, "Exception : \n" + ex.Message);
                return Ok();
            }
        }

        private string ProcessMessageEvent(isRock.LineBot.Message eventMessage, isRock.LineBot.LineUserInfo userInfo)
        {
            string message = string.Empty;
            // reply message

            switch (eventMessage.type.ToLower())
            {
                case "text":
                    message = $"{userInfo.displayName} 說： " + eventMessage.text;
                    break;
                case "location":
                    message = $"{userInfo.displayName} 指定的地址： 經度 {eventMessage.latitude} 緯度 {eventMessage.longitude}";
                    break;
                case "sticker":
                    message = $"{userInfo.displayName} 貼圖： Package {eventMessage.packageId} Id{eventMessage.stickerId}";
                    break;
                case "image":
                    string path = _hostingEnvironment.ContentRootPath + @"\Temp\";
                    var filename = Guid.NewGuid().ToString() + ".jpg";
                    var filebody = isRock.LineBot.Utility.GetUserUploadedContent(eventMessage.id, token);
                    System.IO.File.WriteAllBytes(path + filename, filebody);
                    var fileurl = $"{Request.Scheme}://{Request.Host}/temp/{filename}";
                    message = $"圖片已經上傳主機，下載網址：\n{fileurl}";
                    break;
                
                default:
                    message = "您傳送的型態尚未支援";
                    break;
            }

            return message;
        }

        [HttpPost]
        public ActionResult Post([FromBody]string json)
        {
            var bot = new isRock.LineBot.Bot(token);
            isRock.LineBot.MessageBase responseMessage = null;

            // Message collection for multi message response
            List<isRock.LineBot.MessageBase> responseMessages = new List<isRock.LineBot.MessageBase>();

            try
            {
                // 取得 http post raw data 

                // Parsing JSON
                var receiveMessage = isRock.LineBot.Utility.Parsing(json);
                // Get Line Event
                var lineEvent = receiveMessage.events.FirstOrDefault();
                // prepare for reply message
                if (string.Equals(lineEvent.type, "message", StringComparison.OrdinalIgnoreCase))
                {
                    switch (lineEvent.message.type.ToLower())
                    {
                        case "text":
                            // add text message
                            responseMessage = new isRock.LineBot.TextMessage($"you said : {lineEvent.message.text}");
                            responseMessages.Add(responseMessage);
                            // add ButtonsTemplate if user say "/Show ButtonsTemplate"
                            if (lineEvent.message.text.ToLower().Contains("/show buttonstemplate"))
                            {
                                // define actions
                                var act1 = new isRock.LineBot.MessageAction
                                {
                                    text = "text action1", label = "action1 label"
                                };
                                var act2 = new isRock.LineBot.MessageAction
                                {
                                    text = "text action2", label = "action2 label"
                                };

                                var template = new isRock.LineBot.ButtonsTemplate
                                {
                                    text = "Button Template text",
                                    title = "按鈕標題",
                                    thumbnailImageUrl = new Uri("https://i.imgur.com/wVpGCoP.png")
                                };
                                template.actions.Add(act1);
                                template.actions.Add(act2);
                                // add Template Message into response
                                responseMessages.Add(new isRock.LineBot.TemplateMessage(template));
                            }
                            break;

                        case "sticker":
                            responseMessage = new isRock.LineBot.StickerMessage(1, 2);
                            responseMessages.Add(responseMessage);
                            break;

                        default:
                            responseMessage = new isRock.LineBot.TextMessage($"None handled message type : { lineEvent.message.type}");
                            responseMessages.Add(responseMessage);
                            break;
                    }
                }
                else
                {
                    responseMessage = new isRock.LineBot.TextMessage($"無法處理 event type : { lineEvent.type}");
                    responseMessages.Add(responseMessage);
                }
                bot.ReplyMessage(lineEvent.replyToken, responseMessages);
                return Ok();
            }
            catch(Exception ex)
            {
                // 如果有錯誤， PUSH 給 ADMIN
                bot.PushMessage(adminId, "Exception : \n" + ex.Message);
                return Ok();
            }
        }


        public static String GetHash(String text, String key)
        {
            // change according to your needs, an UTF8Encoding
            // could be more suitable in certain situations
            ASCIIEncoding encoding = new ASCIIEncoding();

            Byte[] textBytes = encoding.GetBytes(text);
            Byte[] keyBytes = encoding.GetBytes(key);

            Byte[] hashBytes;

            using (HMACSHA256 hash = new HMACSHA256(keyBytes))
                hashBytes = hash.ComputeHash(textBytes);

            return Convert.ToBase64String(hashBytes);
        }
    }
}