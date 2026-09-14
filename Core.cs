using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace QingYi
{
    internal enum TextLanguage
    {
        Chinese,
        English
    }

    internal static class TextRules
    {
        internal const int MaximumLength = 200;

        internal static string Clean(string text)
        {
            return (text ?? string.Empty).Trim();
        }

        internal static string Validate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "请输入需要翻译的单词或短句。";
            if (text.Length > MaximumLength) return "最多输入 " + MaximumLength + " 个字符。";
            return null;
        }

        internal static TextLanguage Detect(string text)
        {
            foreach (char value in text ?? string.Empty)
            {
                if ((value >= '\u3400' && value <= '\u4DBF') ||
                    (value >= '\u4E00' && value <= '\u9FFF') ||
                    (value >= '\uF900' && value <= '\uFAFF'))
                    return TextLanguage.Chinese;
            }
            return TextLanguage.English;
        }

        internal static string TargetCode(TextLanguage source)
        {
            return source == TextLanguage.Chinese ? "en" : "zh";
        }

        internal static string LanguageName(TextLanguage language)
        {
            return language == TextLanguage.Chinese ? "中文" : "英文";
        }
    }

    internal sealed class Credentials
    {
        internal string AppId;
        internal string SecretKey;

        internal bool IsComplete
        {
            get { return !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(SecretKey); }
        }
    }

    internal static class CredentialStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("QingYi.Credentials.v1");

        internal static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "QingYi",
                    "credentials.dat");
            }
        }

        internal static Credentials Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                byte[] clear = Unprotect(File.ReadAllBytes(FilePath));
                string value = Encoding.UTF8.GetString(clear);
                int separator = value.IndexOf('\n');
                if (separator <= 0) return null;
                Credentials credentials = new Credentials
                {
                    AppId = value.Substring(0, separator).Trim(),
                    SecretKey = value.Substring(separator + 1).Trim()
                };
                return credentials.IsComplete ? credentials : null;
            }
            catch
            {
                return null;
            }
        }

        internal static void Save(Credentials credentials)
        {
            if (credentials == null || !credentials.IsComplete)
                throw new ArgumentException("APP ID 和密钥不能为空。");

            string directory = Path.GetDirectoryName(FilePath);
            Directory.CreateDirectory(directory);
            byte[] clear = Encoding.UTF8.GetBytes(credentials.AppId.Trim() + "\n" + credentials.SecretKey.Trim());
            File.WriteAllBytes(FilePath, Protect(clear));
        }

        internal static byte[] Protect(byte[] clear)
        {
            return ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser);
        }

        internal static byte[] Unprotect(byte[] encrypted)
        {
            return ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        }
    }

    internal static class BaiduSigner
    {
        internal static string Create(string appId, string query, string salt, string secretKey)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(appId + query + salt + secretKey);
            using (MD5 md5 = MD5.Create())
            {
                StringBuilder result = new StringBuilder(32);
                foreach (byte item in md5.ComputeHash(bytes)) result.Append(item.ToString("x2"));
                return result.ToString();
            }
        }

        internal static string Salt()
        {
            byte[] random = new byte[8];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create()) generator.GetBytes(random);
            StringBuilder result = new StringBuilder(16);
            foreach (byte item in random) result.Append(item.ToString("x2"));
            return result.ToString();
        }
    }

    internal sealed class TranslationResult
    {
        internal string Text;
        internal TextLanguage SourceLanguage;
    }

    internal sealed class TranslationException : Exception
    {
        internal TranslationException(string message) : base(message) { }
    }

    internal sealed class BaiduResponse
    {
        public string error_code { get; set; }
        public string error_msg { get; set; }
        public string from { get; set; }
        public string to { get; set; }
        public BaiduTranslation[] trans_result { get; set; }
    }

    internal sealed class BaiduTranslation
    {
        public string src { get; set; }
        public string dst { get; set; }
    }

    internal sealed class BaiduTranslator : IDisposable
    {
        private readonly HttpClient client;
        private readonly bool ownsClient;

        internal BaiduTranslator()
        {
            client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            ownsClient = true;
        }

        internal BaiduTranslator(HttpClient httpClient)
        {
            client = httpClient;
            ownsClient = false;
        }

        internal async Task<TranslationResult> TranslateAsync(Credentials credentials, string query, CancellationToken cancellationToken)
        {
            string validation = TextRules.Validate(query);
            if (validation != null) throw new TranslationException(validation);
            if (credentials == null || !credentials.IsComplete) throw new TranslationException("请先配置百度翻译 APP ID 和密钥。");

            string cleaned = TextRules.Clean(query);
            TextLanguage source = TextRules.Detect(cleaned);
            string salt = BaiduSigner.Salt();
            string sign = BaiduSigner.Create(credentials.AppId, cleaned, salt, credentials.SecretKey);
            List<KeyValuePair<string, string>> form = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("q", cleaned),
                new KeyValuePair<string, string>("from", "auto"),
                new KeyValuePair<string, string>("to", TextRules.TargetCode(source)),
                new KeyValuePair<string, string>("appid", credentials.AppId),
                new KeyValuePair<string, string>("salt", salt),
                new KeyValuePair<string, string>("sign", sign)
            };

            HttpResponseMessage response;
            try
            {
                response = await client.PostAsync(
                    "https://fanyi-api.baidu.com/api/trans/vip/translate",
                    new FormUrlEncodedContent(form),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                if (cancellationToken.IsCancellationRequested) throw;
                throw new TranslationException("请求超时，请检查网络后重试。");
            }
            catch (HttpRequestException)
            {
                throw new TranslationException("无法连接翻译服务，请检查网络后重试。");
            }

            using (response)
            {
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new TranslationException("翻译服务暂时不可用（HTTP " + (int)response.StatusCode + "）。");
                return Parse(json, source);
            }
        }

        internal static TranslationResult Parse(string json, TextLanguage source)
        {
            BaiduResponse value;
            try
            {
                value = new JavaScriptSerializer().Deserialize<BaiduResponse>(json);
            }
            catch
            {
                throw new TranslationException("翻译服务返回了无法识别的数据。");
            }

            if (value == null) throw new TranslationException("翻译服务没有返回结果。");
            if (!string.IsNullOrEmpty(value.error_code))
                throw new TranslationException(ErrorMessage(value.error_code, value.error_msg));
            if (value.trans_result == null || value.trans_result.Length == 0)
                throw new TranslationException("翻译服务没有返回译文。");

            StringBuilder translated = new StringBuilder();
            foreach (BaiduTranslation item in value.trans_result)
            {
                if (item == null || string.IsNullOrEmpty(item.dst)) continue;
                if (translated.Length > 0) translated.AppendLine();
                translated.Append(item.dst);
            }
            if (translated.Length == 0) throw new TranslationException("翻译服务没有返回译文。");
            return new TranslationResult { Text = translated.ToString(), SourceLanguage = source };
        }

        private static string ErrorMessage(string code, string detail)
        {
            switch (code)
            {
                case "52001": return "翻译服务请求超时，请稍后重试。";
                case "52002": return "翻译服务暂时异常，请稍后重试。";
                case "52003": return "百度翻译账户未授权，请检查服务是否已开通。";
                case "54001": return "APP ID 或密钥不正确，请重新配置。";
                case "54003": return "请求过于频繁，请稍后再试。";
                case "54004": return "本月翻译额度或账户余额不足。";
                case "54005": return "输入内容超过翻译服务限制。";
                case "58001": return "当前语言方向暂不受支持。";
                case "58002": return "百度翻译服务已关闭，请到开放平台检查状态。";
                case "90107": return "百度账户认证尚未完成。";
                default: return "翻译失败（错误码 " + code + "）" + (string.IsNullOrEmpty(detail) ? "。" : "：" + detail);
            }
        }

        public void Dispose()
        {
            if (ownsClient) client.Dispose();
        }
    }
}
