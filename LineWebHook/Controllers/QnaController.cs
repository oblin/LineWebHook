using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker;
using Microsoft.Azure.CognitiveServices.Knowledge.QnAMaker.Models;
using Newtonsoft.Json;

namespace LineWebHook.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QnaController : ControllerBase
    {
        const string token = "66seSTjHyMgSXpvYjQv0JSwec+JplIPrbOKGxvSBYd1FOzIv6wZNYJjqkeD/VvZkLQTjB9V6pBzT1RWfUKphrnPM3f1Q/tMOfDBV5dxQEydcCaK1WELa1sq5cn0fkEvJ7aP2aeVsE2+9/qFR++c3vQdB04t89/1O/w1cDnyilFU=";
        const string adminId = "U0096373bcd4f49c93098761794303f96";
        const string qnaEndpoint = "https://qna20191120.azurewebsites.net/qnamaker";
        const string qnaUrl = "https://qna20191120.azurewebsites.net/qnamaker/knowledgebases/85ae587e-9195-4b00-ab71-e90aa7574ed6/generateAnswer";
        const string qnaKey = "8d399141-dff9-4eba-825f-2aa1d3377c1f";
        const string unknowAnswer = "不好意思，您可以換個方式問嗎? 我不太明白您的意思...";
        const string kbId = "85ae587e-9195-4b00-ab71-e90aa7574ed6";

        [HttpPost]
        public ActionResult Post()
        {
            try
            {
                string json;
                using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
                {
                    json = reader.ReadToEndAsync().Result;
                }
                var receivedMessage = isRock.LineBot.Utility.Parsing(json);
                var bot = new isRock.LineBot.Bot(token);

                var eventObject = receivedMessage.events[0];

                //配合Line verify 
                if (eventObject.replyToken == "00000000000000000000000000000000") return Ok();

                if (eventObject.type.ToLower() == "message")
                {
                    var repmsg = string.Empty;
                    if (eventObject.message.type== "text") // 收到文字
                    {
                        //var runtimeClient = new QnAMakerRuntimeClient(new EndpointKeyServiceClientCredentials(qnaKey))
                        //{
                        //    RuntimeEndpoint = qnaEndpoint
                        //};

                        //var responseList = runtimeClient.Runtime.GenerateAnswerAsync(kbId, new QueryDTO { Question = eventObject.message.text.Trim() }).Result;
                        //var answer = responseList.Answers.First();

                        var responseText = unknowAnswer;

                        var answer = GetAnswer(eventObject.message.text.Trim()).Result;

                        if (answer.Score > 0)
                            responseText = answer.Answer;

                        bot.ReplyMessage(eventObject.replyToken, responseText);
                    }
                    if (eventObject.message.type == "sticker") //收到貼圖
                        bot.ReplyMessage(eventObject.replyToken, 1, 2);
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

        async static Task<QnASearchResult> GetAnswer(string question)
        {
            var uri = qnaUrl;

            Console.WriteLine("Get answers " + uri + ".");

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(uri);
                request.Content = new StringContent("{question:'" + question + "'}", Encoding.UTF8, "application/json");

                // NOTE: The value of the header contains the string/text 'EndpointKey ' with the trailing space

                request.Headers.Add("Authorization", "EndpointKey " + qnaKey);
                var response = await client.SendAsync(request);

                var responseBody = await response.Content.ReadAsStringAsync();


                var responseList = JsonConvert.DeserializeObject<QnASearchResultList>(responseBody);

                return responseList.Answers.First();
            }
        }
    }
}