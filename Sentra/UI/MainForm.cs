using System.Runtime.InteropServices;
using Sentra.Application.Embedding;
using Sentra.Infrastructure.Persistence;
using Sentra.Application.Search;

namespace Sentra.UI
{
    public class MainForm : Form
    {
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect,
            int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        private const int HotkeyId = 1;
        private const uint ModControl = 0x0002;
        private const uint VkSpace = 0x20;

        private TextBox _inputBox;
        private readonly EmbeddingDbContext _dbContext = new();
        private readonly EmbeddingClient _embeddingClient = new();
        private readonly ISearchEngine _searchEngine;

        public MainForm()
        {
            RegisterHotKey(Handle, HotkeyId, ModControl, VkSpace);
            Visible = false;
            
            _searchEngine = new VectorSearch(_dbContext, _embeddingClient);
            
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(400, 60);
            BackColor = Color.FromArgb(30, 30, 30);
            Opacity = 0.95;
            TopMost = true;
            Deactivate += (s, e) => Hide();

            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));

            _inputBox = new TextBox
            {
                Width = 360,
                Left = 20,
                Top = 15,
                Font = new Font("Segoe UI", 14),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(45, 45, 45),
                BorderStyle = BorderStyle.None
            };
            _inputBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    OnSendClick(null, null);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    Visible = false;
                    WindowState = FormWindowState.Minimized;
                    _inputBox.Text = string.Empty;
                }
            };

            Controls.Add(_inputBox);
        }

        protected override void WndProc(ref Message m)
        {
            const int wmHotkey = 0x0312;
            if (m.Msg == wmHotkey && m.WParam.ToInt32() == HotkeyId)
            {
                Visible = true;
                WindowState = FormWindowState.Normal;
                CenterToScreen();
                Activate();
                _inputBox.Focus();
            }
            base.WndProc(ref m);
        }

        private async void OnSendClick(object sender, EventArgs e)
        {
            string userInput = _inputBox.Text;
            try
            {
                var results = await _searchEngine.SearchAsync(userInput);
                if (results.Count == 0)
                {
                    MessageBox.Show("Ничего не найдено.");
                }
                else
                {
                    var msg = string.Join("\n\n", results.Select(r => $"📄 {r.FilePath}\n🔍 {r.Snippet[..Math.Min(200, r.Snippet.Length)]}..."));
                    MessageBox.Show(msg, "Результаты поиска", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                Visible = false;
                WindowState = FormWindowState.Minimized;
                _inputBox.Text = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка поиска: " + ex.Message);
            }
        }


        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(Handle, HotkeyId);
            base.OnFormClosing(e);
        }
    }
}
