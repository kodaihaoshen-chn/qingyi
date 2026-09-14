using System;
using System.Drawing;
using System.Globalization;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QingYi
{
    internal static class Theme
    {
        internal static readonly Color Canvas = Color.FromArgb(246, 248, 251);
        internal static readonly Color Paper = Color.White;
        internal static readonly Color Ink = Color.FromArgb(28, 35, 48);
        internal static readonly Color Muted = Color.FromArgb(100, 112, 130);
        internal static readonly Color Accent = Color.FromArgb(48, 104, 232);
        internal static readonly Color Border = Color.FromArgb(216, 222, 232);

        internal static Font Font(float size, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
        }

        internal static Button Button(string text, int width, bool primary)
        {
            Button button = new Button
            {
                Text = text,
                Width = width,
                Height = 34,
                Margin = new Padding(0, 0, 9, 0),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                BackColor = primary ? Accent : Paper,
                ForeColor = primary ? Color.White : Ink,
                Font = Font(9, primary ? FontStyle.Bold : FontStyle.Regular),
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = primary ? Accent : Border;
            button.FlatAppearance.BorderSize = 1;
            return button;
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly TextBox sourceBox;
        private readonly TextBox targetBox;
        private readonly Label countLabel;
        private readonly Label statusLabel;
        private readonly Button translateButton;
        private readonly Button readSourceButton;
        private readonly Button readTargetButton;
        private readonly Button pinButton;
        private readonly SpeechSynthesizer speech;
        private readonly BaiduTranslator translator;
        private CancellationTokenSource translationCancellation;
        private bool closing;

        internal MainForm()
        {
            Text = "轻译";
            Icon = SystemIcons.Information;
            Font = Theme.Font(9, FontStyle.Regular);
            BackColor = Theme.Canvas;
            ForeColor = Theme.Ink;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(520, 510);
            MinimumSize = new Size(440, 475);

            Panel header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Theme.Paper, Padding = new Padding(16, 11, 12, 8) };
            Label title = new Label
            {
                Text = "轻译",
                AutoSize = true,
                Font = Theme.Font(16, FontStyle.Bold),
                ForeColor = Theme.Ink,
                Location = new Point(16, 13)
            };
            pinButton = Theme.Button("置顶", 62, false);
            pinButton.AccessibleName = "切换窗口置顶";
            Button settingsButton = Theme.Button("设置", 62, false);
            settingsButton.AccessibleName = "设置百度翻译密钥";
            FlowLayoutPanel headerActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 145,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 1, 0, 0)
            };
            headerActions.Controls.Add(pinButton);
            headerActions.Controls.Add(settingsButton);
            header.Controls.Add(title);
            header.Controls.Add(headerActions);

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 14, 16, 14),
                ColumnCount = 1,
                RowCount = 7,
                BackColor = Theme.Canvas
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 47));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 47));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            Controls.Add(layout);
            Controls.Add(header);

            Panel sourceHeader = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            Label sourceLabel = new Label { Text = "输入", AutoSize = true, Font = Theme.Font(10, FontStyle.Bold), ForeColor = Theme.Ink, Location = new Point(0, 2) };
            countLabel = new Label { Text = "0 / 200", AutoSize = true, ForeColor = Theme.Muted, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            sourceHeader.Controls.Add(sourceLabel);
            sourceHeader.Controls.Add(countLabel);
            sourceHeader.Resize += delegate { countLabel.Location = new Point(Math.Max(0, sourceHeader.ClientSize.Width - countLabel.Width), 4); };
            layout.Controls.Add(sourceHeader, 0, 0);

            sourceBox = CreateTextBox(false);
            sourceBox.MaxLength = TextRules.MaximumLength;
            sourceBox.AccessibleName = "待翻译文本";
            layout.Controls.Add(sourceBox, 0, 1);

            FlowLayoutPanel sourceActions = ActionRow();
            translateButton = Theme.Button("翻译", 92, true);
            translateButton.AccessibleName = "翻译";
            readSourceButton = Theme.Button("朗读原文", 96, false);
            readSourceButton.AccessibleName = "朗读原文一次";
            sourceActions.Controls.Add(translateButton);
            sourceActions.Controls.Add(readSourceButton);
            layout.Controls.Add(sourceActions, 0, 2);

            Label targetLabel = new Label
            {
                Text = "译文",
                Dock = DockStyle.Fill,
                Font = Theme.Font(10, FontStyle.Bold),
                ForeColor = Theme.Ink,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0)
            };
            layout.Controls.Add(targetLabel, 0, 3);

            targetBox = CreateTextBox(true);
            targetBox.AccessibleName = "翻译结果";
            layout.Controls.Add(targetBox, 0, 4);

            FlowLayoutPanel targetActions = ActionRow();
            Button copyButton = Theme.Button("复制译文", 96, false);
            copyButton.AccessibleName = "复制译文";
            readTargetButton = Theme.Button("朗读译文", 96, false);
            readTargetButton.AccessibleName = "朗读译文一次";
            targetActions.Controls.Add(copyButton);
            targetActions.Controls.Add(readTargetButton);
            layout.Controls.Add(targetActions, 0, 5);

            statusLabel = new Label
            {
                Text = "输入单词或短句，按回车或点击“翻译”",
                Dock = DockStyle.Fill,
                ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                Margin = new Padding(0)
            };
            statusLabel.AccessibleName = "状态";
            layout.Controls.Add(statusLabel, 0, 6);

            speech = new SpeechSynthesizer();
            translator = new BaiduTranslator();

            sourceBox.TextChanged += delegate
            {
                countLabel.Text = sourceBox.TextLength + " / " + TextRules.MaximumLength;
                countLabel.ForeColor = sourceBox.TextLength >= TextRules.MaximumLength ? Color.FromArgb(180, 72, 48) : Theme.Muted;
            };
            sourceBox.KeyDown += async delegate(object sender, KeyEventArgs e)
            {
                if (!IsTranslateKey(e.KeyCode)) return;
                e.Handled = true;
                e.SuppressKeyPress = true;
                await TranslateNow();
            };
            translateButton.Click += async delegate { await TranslateNow(); };
            readSourceButton.Click += delegate { ReadOnce(sourceBox.Text, TextRules.Detect(sourceBox.Text)); };
            readTargetButton.Click += delegate
            {
                TextLanguage source = TextRules.Detect(sourceBox.Text);
                ReadOnce(targetBox.Text, source == TextLanguage.Chinese ? TextLanguage.English : TextLanguage.Chinese);
            };
            copyButton.Click += delegate
            {
                if (string.IsNullOrWhiteSpace(targetBox.Text)) { SetStatus("当前没有可复制的译文。", true); return; }
                try { Clipboard.SetText(targetBox.Text); SetStatus("译文已复制。", false); }
                catch (Exception ex) { SetStatus("复制失败：" + ex.Message, true); }
            };
            pinButton.Click += delegate
            {
                TopMost = !TopMost;
                pinButton.Text = TopMost ? "已置顶" : "置顶";
                SetStatus(TopMost ? "窗口已置顶。" : "已取消置顶。", false);
            };
            settingsButton.Click += delegate { ShowCredentialDialog(); };
            speech.SpeakCompleted += delegate
            {
                if (closing || IsDisposed) return;
                BeginInvoke((MethodInvoker)delegate { readSourceButton.Enabled = true; readTargetButton.Enabled = true; });
            };
            Shown += delegate
            {
                sourceBox.Focus();
                if (CredentialStore.Load() == null) BeginInvoke((MethodInvoker)delegate { ShowCredentialDialog(); });
            };
            FormClosing += delegate
            {
                closing = true;
                if (translationCancellation != null) translationCancellation.Cancel();
                try { speech.SpeakAsyncCancelAll(); } catch { }
                speech.Dispose();
                translator.Dispose();
            };
        }

        private static TextBox CreateTextBox(bool readOnly)
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = readOnly,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.Paper,
                ForeColor = Theme.Ink,
                Font = Theme.Font(11, FontStyle.Regular),
                Margin = new Padding(0, 0, 0, 8),
                AcceptsReturn = true
            };
        }

        private static FlowLayoutPanel ActionRow()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 6, 0, 6),
                Margin = new Padding(0)
            };
        }

        private async Task TranslateNow()
        {
            if (!translateButton.Enabled) return;
            string text = TextRules.Clean(sourceBox.Text);
            string validation = TextRules.Validate(text);
            if (validation != null) { SetStatus(validation, true); return; }

            Credentials credentials = CredentialStore.Load();
            if (credentials == null)
            {
                if (!ShowCredentialDialog()) return;
                credentials = CredentialStore.Load();
                if (credentials == null) return;
            }

            if (translationCancellation != null) translationCancellation.Cancel();
            translationCancellation = new CancellationTokenSource();
            translateButton.Enabled = false;
            targetBox.Text = string.Empty;
            SetStatus("正在翻译…", false);
            try
            {
                TranslationResult result = await translator.TranslateAsync(credentials, text, translationCancellation.Token);
                if (closing) return;
                targetBox.Text = result.Text;
                TextLanguage target = result.SourceLanguage == TextLanguage.Chinese ? TextLanguage.English : TextLanguage.Chinese;
                SetStatus("已完成：" + TextRules.LanguageName(result.SourceLanguage) + " → " + TextRules.LanguageName(target), false);
            }
            catch (OperationCanceledException)
            {
                if (!closing) SetStatus("翻译已取消。", true);
            }
            catch (TranslationException ex)
            {
                SetStatus(ex.Message, true);
            }
            catch (Exception)
            {
                SetStatus("翻译失败，请稍后重试。", true);
            }
            finally
            {
                if (!closing && !IsDisposed) translateButton.Enabled = true;
            }
        }

        internal static bool IsTranslateKey(Keys keyCode)
        {
            return keyCode == Keys.Enter;
        }

        private void ReadOnce(string value, TextLanguage language)
        {
            string text = TextRules.Clean(value);
            if (text.Length == 0) { SetStatus("当前没有可朗读的内容。", true); return; }
            try
            {
                InstalledVoice match = null;
                foreach (InstalledVoice voice in speech.GetInstalledVoices())
                {
                    if (!voice.Enabled) continue;
                    string code = voice.VoiceInfo.Culture.TwoLetterISOLanguageName;
                    if ((language == TextLanguage.Chinese && code == "zh") || (language == TextLanguage.English && code == "en"))
                    {
                        match = voice;
                        break;
                    }
                }
                if (match == null)
                {
                    SetStatus("Windows 未安装可用的" + TextRules.LanguageName(language) + "语音。", true);
                    return;
                }
                speech.SpeakAsyncCancelAll();
                speech.SelectVoice(match.VoiceInfo.Name);
                speech.Rate = 0;
                readSourceButton.Enabled = false;
                readTargetButton.Enabled = false;
                speech.SpeakAsync(text);
                SetStatus("正在朗读" + TextRules.LanguageName(language) + "…", false);
            }
            catch (Exception ex)
            {
                readSourceButton.Enabled = true;
                readTargetButton.Enabled = true;
                SetStatus("无法朗读：" + ex.Message, true);
            }
        }

        private bool ShowCredentialDialog()
        {
            using (CredentialForm dialog = new CredentialForm(CredentialStore.Load()))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) { SetStatus("尚未配置百度翻译密钥。", true); return false; }
                try
                {
                    CredentialStore.Save(dialog.Credentials);
                    SetStatus("百度翻译密钥已加密保存。", false);
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "无法保存密钥：" + ex.Message, "轻译", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
        }

        private void SetStatus(string value, bool error)
        {
            if (closing || IsDisposed) return;
            statusLabel.Text = value;
            statusLabel.ForeColor = error ? Color.FromArgb(180, 72, 48) : Theme.Muted;
        }
    }

    internal sealed class CredentialForm : Form
    {
        private readonly TextBox appIdBox;
        private readonly TextBox secretBox;

        internal Credentials Credentials
        {
            get { return new Credentials { AppId = appIdBox.Text.Trim(), SecretKey = secretBox.Text.Trim() }; }
        }

        internal CredentialForm(Credentials current)
        {
            Text = "设置百度翻译";
            Icon = SystemIcons.Information;
            Font = Theme.Font(9, FontStyle.Regular);
            BackColor = Theme.Canvas;
            ForeColor = Theme.Ink;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(430, 285);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);

            Label title = new Label { Text = "首次使用只需配置一次", Font = Theme.Font(14, FontStyle.Bold), AutoSize = true, Location = new Point(20, 18) };
            Label note = new Label
            {
                Text = "请输入百度翻译开放平台的 APP ID 和密钥。\r\n凭据将由当前 Windows 账户加密保存。",
                ForeColor = Theme.Muted,
                AutoSize = true,
                Location = new Point(21, 55)
            };
            Label appIdLabel = new Label { Text = "APP ID", AutoSize = true, Location = new Point(21, 104) };
            appIdBox = new TextBox { Location = new Point(21, 125), Width = 388, Height = 27, BorderStyle = BorderStyle.FixedSingle };
            appIdBox.AccessibleName = "百度翻译 APP ID";
            Label secretLabel = new Label { Text = "密钥", AutoSize = true, Location = new Point(21, 162) };
            secretBox = new TextBox { Location = new Point(21, 183), Width = 388, Height = 27, BorderStyle = BorderStyle.FixedSingle, UseSystemPasswordChar = true };
            secretBox.AccessibleName = "百度翻译密钥";
            CheckBox showSecret = new CheckBox { Text = "显示密钥", AutoSize = true, Location = new Point(22, 216), ForeColor = Theme.Muted };
            Button cancel = Theme.Button("取消", 78, false);
            cancel.Location = new Point(244, 238);
            cancel.DialogResult = DialogResult.Cancel;
            Button save = Theme.Button("保存", 78, true);
            save.Location = new Point(331, 238);
            save.DialogResult = DialogResult.OK;

            if (current != null)
            {
                appIdBox.Text = current.AppId;
                secretBox.Text = current.SecretKey;
            }

            showSecret.CheckedChanged += delegate { secretBox.UseSystemPasswordChar = !showSecret.Checked; };
            save.Click += delegate(object sender, EventArgs e)
            {
                if (!Credentials.IsComplete)
                {
                    MessageBox.Show(this, "APP ID 和密钥不能为空。", "轻译", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.None;
                }
            };

            Controls.AddRange(new Control[] { title, note, appIdLabel, appIdBox, secretLabel, secretBox, showSecret, cancel, save });
            AcceptButton = save;
            CancelButton = cancel;
        }
    }
}
