using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QingYi
{
    internal static class Tests
    {
        private static int passed;
        private static int failed;

        [STAThread]
        private static int Main()
        {
            Run("中文识别", delegate { Equal(TextLanguage.Chinese, TextRules.Detect("你好")); });
            Run("英文识别", delegate { Equal(TextLanguage.English, TextRules.Detect("hello")); });
            Run("数字按英文方向", delegate { Equal(TextLanguage.English, TextRules.Detect("123")); });
            Run("中文目标是英文", delegate { Equal("en", TextRules.TargetCode(TextLanguage.Chinese)); });
            Run("英文目标是中文", delegate { Equal("zh", TextRules.TargetCode(TextLanguage.English)); });
            Run("清理首尾空白", delegate { Equal("word", TextRules.Clean("  word \r\n")); });
            Run("拒绝空输入", delegate { True(TextRules.Validate("  ") != null); });
            Run("接受200字符", delegate { Equal(null, TextRules.Validate(new string('a', 200))); });
            Run("拒绝201字符", delegate { True(TextRules.Validate(new string('a', 201)) != null); });
            Run("签名稳定", delegate { Equal("f89f9594663708c1605f3d736d01d2d4", BaiduSigner.Create("2015063000000001", "apple", "1435660288", "12345678")); });
            Run("盐长度", delegate { Equal(16, BaiduSigner.Salt().Length); });
            Run("凭据加密往返", CredentialRoundTrip);
            Run("解析单项译文", ParseSingle);
            Run("解析多项译文", ParseMultiple);
            Run("解析API错误", ParseError);
            Run("拒绝无结果响应", ParseEmpty);
            Run("请求字段和方向", RequestShape);
            Run("窗口默认不置顶", FormDefaults);
            Run("窗口输入限制200字符", FormInputLimit);
            Run("窗口置顶开关", FormPinToggle);
            Run("译文框只读", FormTargetReadOnly);
            Run("密钥默认隐藏", CredentialSecretHidden);
            Run("回车触发翻译", delegate { Equal(true, MainForm.IsTranslateKey(Keys.Enter)); });
            Run("其他按键不触发翻译", delegate { Equal(false, MainForm.IsTranslateKey(Keys.Space)); });

            Console.WriteLine("通过 " + passed + "，失败 " + failed);
            return failed == 0 ? 0 : 1;
        }

        private static void CredentialRoundTrip()
        {
            byte[] clear = Encoding.UTF8.GetBytes("appid\nsecret");
            byte[] encrypted = CredentialStore.Protect(clear);
            True(Convert.ToBase64String(clear) != Convert.ToBase64String(encrypted));
            Equal("appid\nsecret", Encoding.UTF8.GetString(CredentialStore.Unprotect(encrypted)));
        }

        private static void ParseSingle()
        {
            TranslationResult result = BaiduTranslator.Parse("{\"from\":\"en\",\"to\":\"zh\",\"trans_result\":[{\"src\":\"apple\",\"dst\":\"苹果\"}]}", TextLanguage.English);
            Equal("苹果", result.Text);
            Equal(TextLanguage.English, result.SourceLanguage);
        }

        private static void ParseMultiple()
        {
            TranslationResult result = BaiduTranslator.Parse("{\"trans_result\":[{\"dst\":\"第一行\"},{\"dst\":\"第二行\"}]}", TextLanguage.English);
            Equal("第一行" + Environment.NewLine + "第二行", result.Text);
        }

        private static void ParseError()
        {
            bool thrown = false;
            try { BaiduTranslator.Parse("{\"error_code\":\"54001\",\"error_msg\":\"Invalid Sign\"}", TextLanguage.English); }
            catch (TranslationException ex) { thrown = ex.Message.Contains("密钥"); }
            True(thrown);
        }

        private static void ParseEmpty()
        {
            bool thrown = false;
            try { BaiduTranslator.Parse("{\"trans_result\":[]}", TextLanguage.English); }
            catch (TranslationException) { thrown = true; }
            True(thrown);
        }

        private static void RequestShape()
        {
            RecordingHandler handler = new RecordingHandler();
            using (HttpClient http = new HttpClient(handler))
            using (BaiduTranslator translator = new BaiduTranslator(http))
            {
                TranslationResult result = translator.TranslateAsync(
                    new Credentials { AppId = "app", SecretKey = "secret" },
                    "apple",
                    CancellationToken.None).GetAwaiter().GetResult();
                Equal("苹果", result.Text);
                True(handler.Body.Contains("q=apple"));
                True(handler.Body.Contains("from=auto"));
                True(handler.Body.Contains("to=zh"));
                True(handler.Body.Contains("appid=app"));
                True(handler.Body.Contains("sign="));
            }
        }

        private static void FormDefaults()
        {
            using (MainForm form = new MainForm())
            {
                Equal("轻译", form.Text);
                Equal(false, form.TopMost);
                True(form.ClientSize.Width >= 440);
            }
        }

        private static void FormInputLimit()
        {
            using (MainForm form = new MainForm())
            {
                TextBox input = (TextBox)Find(form, "待翻译文本");
                Equal(200, input.MaxLength);
            }
        }

        private static void FormPinToggle()
        {
            using (MainForm form = new MainForm())
            {
                Button pin = (Button)Find(form, "切换窗口置顶");
                RaiseClick(pin);
                Equal(true, form.TopMost);
                RaiseClick(pin);
                Equal(false, form.TopMost);
            }
        }

        private static void FormTargetReadOnly()
        {
            using (MainForm form = new MainForm())
            {
                TextBox target = (TextBox)Find(form, "翻译结果");
                Equal(true, target.ReadOnly);
            }
        }

        private static void CredentialSecretHidden()
        {
            using (CredentialForm form = new CredentialForm(null))
            {
                TextBox secret = (TextBox)Find(form, "百度翻译密钥");
                Equal(true, secret.UseSystemPasswordChar);
            }
        }

        private static Control Find(Control root, string accessibleName)
        {
            foreach (Control child in root.Controls)
            {
                if (child.AccessibleName == accessibleName) return child;
                Control nested = Find(child, accessibleName);
                if (nested != null) return nested;
            }
            return null;
        }

        private static void RaiseClick(Button button)
        {
            typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(button, new object[] { EventArgs.Empty });
        }

        private static void Run(string name, Action test)
        {
            try { test(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + " - " + ex.Message); }
        }

        private static void Equal(object expected, object actual)
        {
            if (!object.Equals(expected, actual)) throw new Exception("期望 " + expected + "，实际 " + actual);
        }

        private static void True(bool value)
        {
            if (!value) throw new Exception("条件不成立");
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            internal string Body;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent("{\"trans_result\":[{\"dst\":\"苹果\"}]}", Encoding.UTF8, "application/json");
                return Task.FromResult(response);
            }
        }
    }
}
