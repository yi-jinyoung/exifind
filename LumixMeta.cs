using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("Exifind")]
[assembly: AssemblyProduct("Exifind")]
[assembly: AssemblyDescription("Photo and video metadata inspector")]
[assembly: AssemblyCompany("scenes.by")]
[assembly: AssemblyCopyright("Copyright © 2026 scenes.by")]
[assembly: AssemblyVersion("1.5.0.0")]
[assembly: AssemblyFileVersion("1.5.0.0")]

namespace LumixMetaApp
{
    sealed class MediaItem
    {
        public readonly string Path;
        public MediaItem(string path) { Path = path; }
        public override string ToString() { return System.IO.Path.GetFileName(Path); }
    }

    sealed class BrandHeader : Panel
    {
        readonly Font titleFont;
        readonly Font taglineFont;

        public BrandHeader(FontFamily family)
        {
            titleFont = new Font(family, 18, FontStyle.Bold, GraphicsUnit.Point);
            taglineFont = new Font(family, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(243, 243, 239);
            Dock = DockStyle.Fill;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            TextRenderer.DrawText(e.Graphics, "Exifind", titleFont, new Point(22, 8), Color.FromArgb(20, 21, 19), flags);
            TextRenderer.DrawText(e.Graphics, "Find the Unseen Details", taglineFont, new Point(22, 40), Color.FromArgb(95, 97, 91), flags);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { titleFont.Dispose(); taglineFont.Dispose(); }
            base.Dispose(disposing);
        }
    }

    public sealed class MainForm : Form
    {
        readonly Panel welcome = new Panel();
        readonly TableLayoutPanel shell = new TableLayoutPanel();
        readonly Label dropTitle = new Label();
        readonly ListBox files = new ListBox();
        readonly TextBox selectedPath = new TextBox();
        readonly PictureBox preview = new PictureBox();
        readonly Label fileTitle = new Label();
        readonly Label cameraTitle = new Label();
        readonly TabControl tabs = new TabControl();
        readonly DataGridView summary = new DataGridView();
        readonly DataGridView lut = new DataGridView();
        readonly DataGridView all = new DataGridView();
        readonly TextBox search = new TextBox();
        readonly Dictionary<string, Dictionary<string, object>> metadata =
            new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        string currentFile;
        readonly PrivateFontCollection privateFonts = new PrivateFontCollection();
        readonly PrivateFontCollection thinFonts = new PrivateFontCollection();
        readonly FontFamily uiFont;
        readonly FontFamily thinFont;
        readonly string runtimeDir;

        public MainForm()
        {
            Text = "Exifind";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            MinimumSize = new Size(900, 620);
            Size = new Size(1120, 760);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(243, 243, 239);
            runtimeDir = EnsureBundledRuntime();
            uiFont = ResolveUiFont();
            thinFont = ResolveThinFont();
            Font = new Font(uiFont, 10.5f, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            AllowDrop = true;
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;

            shell.Dock = DockStyle.Fill;
            shell.ColumnCount = 1;
            shell.RowCount = 4;
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            Controls.Add(shell);

            var header = new BrandHeader(uiFont);
            shell.Controls.Add(header, 0, 0);

            var footer = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(243, 243, 239) };
            var copyright = new LinkLabel {
                Text = "Copyright © 2026 scenes.by", Dock = DockStyle.Right, Width = 230,
                TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 16, 0),
                Font = new Font(uiFont, 9), ForeColor = Color.FromArgb(110, 112, 106),
                LinkColor = Color.FromArgb(90, 92, 87), ActiveLinkColor = Color.FromArgb(32, 34, 31),
                VisitedLinkColor = Color.FromArgb(90, 92, 87), LinkBehavior = LinkBehavior.HoverUnderline,
                BackColor = Color.FromArgb(243, 243, 239)
            };
            copyright.LinkClicked += delegate {
                Process.Start(new ProcessStartInfo("https://www.instagram.com/scenes.by/") { UseShellExecute = true });
            };
            var licenses = new LinkLabel {
                Text = "Open-source licenses", Dock = DockStyle.Left, Width = 150,
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(16, 0, 0, 0),
                Font = new Font(uiFont, 9), LinkColor = Color.FromArgb(90, 92, 87),
                ActiveLinkColor = Color.FromArgb(32, 34, 31), LinkBehavior = LinkBehavior.HoverUnderline,
                BackColor = Color.FromArgb(243, 243, 239)
            };
            licenses.LinkClicked += delegate {
                string notice = Path.Combine(runtimeDir, "licenses", "THIRD-PARTY-NOTICES.txt");
                if (File.Exists(notice)) Process.Start(new ProcessStartInfo(notice) { UseShellExecute = true });
            };
            footer.Controls.Add(copyright);
            footer.Controls.Add(licenses);
            shell.Controls.Add(footer, 0, 3);

            BuildWelcome();
            BuildWorkspace();
            ShowWelcome();
        }

        void BuildWelcome()
        {
            welcome.Dock = DockStyle.Fill;
            welcome.BackColor = Color.FromArgb(234, 236, 227);
            welcome.Padding = new Padding(18, 12, 18, 12);
            welcome.AllowDrop = true;
            welcome.DragEnter += OnDragEnter;
            welcome.DragDrop += OnDragDrop;

            dropTitle.Text = "Drop media here";
            dropTitle.Font = new Font(thinFont, 13, FontStyle.Regular);
            dropTitle.TextAlign = ContentAlignment.MiddleLeft;
            dropTitle.Dock = DockStyle.Fill;
            dropTitle.Padding = new Padding(18, 0, 0, 0);

            var hint = new Label {
                Text = "PHOTO  JPG · PNG · HEIF · TIFF · WebP · AVIF · RAW\nVIDEO  MP4 · MOV · M4V · AVI · MKV · WebM · MTS · M2TS · MPEG · 3GP",
                TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Right,
                Width = 570, Font = new Font(uiFont, 9), ForeColor = Color.FromArgb(95, 97, 91)
            };
            var chooseHost = new Panel { Dock = DockStyle.Right, Width = 116 };
            var choose = new Button {
                Text = "Choose files", Width = 102, Height = 34,
                BackColor = Color.FromArgb(32, 34, 31), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Location = new Point(7, 18)
            };
            choose.FlatAppearance.BorderSize = 0;
            choose.Click += delegate { ChooseFiles(); };
            chooseHost.Controls.Add(choose);

            welcome.Controls.Add(dropTitle);
            welcome.Controls.Add(hint);
            welcome.Controls.Add(chooseHost);
            shell.Controls.Add(welcome, 0, 1);
        }

        void BuildWorkspace()
        {
            var workspace = new TableLayoutPanel {
                Name = "workspace", Dock = DockStyle.Fill, BackColor = Color.White,
                Visible = true, ColumnCount = 2, RowCount = 1
            };
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
            workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            workspace.AllowDrop = true;
            workspace.DragEnter += OnDragEnter;
            workspace.DragDrop += OnDragDrop;

            var filePanel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            filePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            filePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            filePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            var detailPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18) };

            var filesHeading = new Label {
                Text = "MEDIA FILES", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(uiFont, 9, FontStyle.Bold), ForeColor = Color.FromArgb(95, 97, 91)
            };
            files.Dock = DockStyle.Fill;
            files.BorderStyle = BorderStyle.None;
            files.HorizontalScrollbar = true;
            files.Font = new Font(uiFont, 10.5f);
            files.SelectedIndexChanged += delegate {
                var item = files.SelectedItem as MediaItem;
                if (item != null) {
                    selectedPath.Text = item.Path;
                    DisplayFile(item.Path);
                }
            };
            var pathPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            pathPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            pathPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pathPanel.Controls.Add(new Label {
                Text = "FILE PATH", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(uiFont, 9, FontStyle.Bold), ForeColor = Color.FromArgb(95, 97, 91)
            }, 0, 0);
            selectedPath.Dock = DockStyle.Fill;
            selectedPath.ReadOnly = true;
            selectedPath.Multiline = true;
            selectedPath.WordWrap = true;
            selectedPath.BackColor = Color.FromArgb(247, 247, 244);
            selectedPath.BorderStyle = BorderStyle.FixedSingle;
            selectedPath.Font = new Font(uiFont, 9);
            selectedPath.Cursor = Cursors.Hand;
            selectedPath.Click += delegate {
                if (!String.IsNullOrEmpty(currentFile) && File.Exists(currentFile))
                    Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + currentFile + "\"") { UseShellExecute = true });
            };
            pathPanel.Controls.Add(selectedPath, 0, 1);
            filePanel.Controls.Add(filesHeading, 0, 0);
            filePanel.Controls.Add(files, 0, 1);
            filePanel.Controls.Add(pathPanel, 0, 2);

            var hero = new Panel { Dock = DockStyle.Top, Height = 126, BackColor = Color.FromArgb(41, 43, 40) };
            preview.Size = new Size(138, 98);
            preview.Location = new Point(14, 14);
            preview.SizeMode = PictureBoxSizeMode.Zoom;
            preview.BackColor = Color.FromArgb(20, 20, 20);
            fileTitle.Location = new Point(170, 25);
            fileTitle.Size = new Size(650, 38);
            fileTitle.Font = new Font(uiFont, 15, FontStyle.Bold);
            fileTitle.ForeColor = Color.White;
            cameraTitle.Location = new Point(170, 66);
            cameraTitle.Size = new Size(650, 48);
            cameraTitle.Font = new Font(uiFont, 10.5f);
            cameraTitle.ForeColor = Color.FromArgb(200, 202, 195);
            hero.Controls.Add(preview);
            hero.Controls.Add(fileTitle);
            hero.Controls.Add(cameraTitle);

            tabs.Dock = DockStyle.Fill;
            var summaryTab = new TabPage("Summary");
            var lutTab = new TabPage("Style");
            var allTab = new TabPage("All metadata");
            SetupGrid(summary);
            SetupGrid(lut);
            summaryTab.Controls.Add(summary);
            lutTab.Controls.Add(lut);

            search.Dock = DockStyle.Top;
            search.Text = "";
            search.TextChanged += delegate { FillAllGrid(); };
            all.Dock = DockStyle.Fill;
            all.ReadOnly = true;
            all.AllowUserToAddRows = false;
            all.AllowUserToDeleteRows = false;
            all.RowHeadersVisible = false;
            all.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            all.Columns.Add("tag", "Tag");
            all.Columns.Add("value", "Value");
            allTab.Controls.Add(all);
            allTab.Controls.Add(search);
            tabs.TabPages.Add(summaryTab);
            tabs.TabPages.Add(lutTab);
            tabs.TabPages.Add(allTab);

            detailPanel.Controls.Add(tabs);
            detailPanel.Controls.Add(hero);
            workspace.Controls.Add(filePanel, 0, 0);
            workspace.Controls.Add(detailPanel, 1, 0);
            shell.Controls.Add(workspace, 0, 2);
        }

        void SetupGrid(DataGridView grid)
        {
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.RowHeadersVisible = false;
            grid.ColumnHeadersVisible = false;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.DefaultCellStyle.Font = new Font(uiFont, 10.5f, FontStyle.Regular, GraphicsUnit.Point);
            grid.DefaultCellStyle.Padding = new Padding(3, 2, 3, 2);
            grid.RowTemplate.Height = 26;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.Columns.Add("name", "Name");
            grid.Columns.Add("value", "Value");
            grid.Columns[0].FillWeight = 35;
            grid.Columns[1].FillWeight = 65;
        }

        void ShowWelcome()
        {
            welcome.Visible = true;
            var workspace = Controls.Find("workspace", true).FirstOrDefault();
            if (workspace != null) workspace.Visible = true;
        }

        void ShowWorkspace()
        {
            welcome.Visible = true;
            var workspace = Controls.Find("workspace", true).FirstOrDefault();
            if (workspace != null) workspace.Visible = true;
        }

        void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effect = DragDropEffects.Copy;
                dropTitle.Text = "Release to analyze";
            }
        }

        void OnDragDrop(object sender, DragEventArgs e)
        {
            dropTitle.Text = "Drop media here";
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths != null) AddFiles(paths);
        }

        void ChooseFiles()
        {
            using (var dialog = new OpenFileDialog()) {
                dialog.Multiselect = true;
                dialog.Filter = "Supported media|*.jpg;*.jpeg;*.png;*.heic;*.heif;*.tif;*.tiff;*.webp;*.avif;*.bmp;*.gif;*.arw;*.cr2;*.cr3;*.nef;*.nrw;*.orf;*.rw2;*.raf;*.dng;*.rwl;*.pef;*.srw;*.3fr;*.iiq;*.x3f;*.gpr;*.raw;*.mp4;*.mov;*.m4v;*.avi;*.mkv;*.webm;*.mts;*.m2ts;*.mpg;*.mpeg;*.3gp|Images|*.jpg;*.jpeg;*.png;*.heic;*.heif;*.tif;*.tiff;*.webp;*.avif;*.bmp;*.gif;*.arw;*.cr2;*.cr3;*.nef;*.nrw;*.orf;*.rw2;*.raf;*.dng;*.rwl;*.pef;*.srw;*.3fr;*.iiq;*.x3f;*.gpr;*.raw|Videos|*.mp4;*.mov;*.m4v;*.avi;*.mkv;*.webm;*.mts;*.m2ts;*.mpg;*.mpeg;*.3gp|All files|*.*";
                if (dialog.ShowDialog(this) == DialogResult.OK) AddFiles(dialog.FileNames);
            }
        }

        void AddFiles(IEnumerable<string> paths)
        {
            foreach (var path in paths.Where(File.Exists)) {
                if (!metadata.ContainsKey(path)) {
                    try {
                        metadata[path] = ReadMetadata(path);
                        files.Items.Add(new MediaItem(path));
                    } catch (Exception ex) {
                        MessageBox.Show(this, ex.Message, "Could not read metadata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            if (files.Items.Count > 0) {
                ShowWorkspace();
                files.SelectedIndex = files.Items.Count - 1;
            }
        }

        Dictionary<string, object> ReadMetadata(string path)
        {
            string exiftool = FindExifTool();
            var start = new ProcessStartInfo {
                FileName = exiftool,
                Arguments = "-json -G1 -a -u \"" + path.Replace("\"", "\\\"") + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            using (var process = Process.Start(start)) {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new Exception(String.IsNullOrWhiteSpace(error) ? "ExifTool failed." : error);
                var serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
                var list = serializer.Deserialize<List<Dictionary<string, object>>>(output);
                if (list == null || list.Count == 0)
                    throw new Exception("ExifTool returned no metadata.");
                return list[0];
            }
        }

        string FindExifTool()
        {
            string[] paths = {
                Path.Combine(runtimeDir, "exiftool.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exiftool.exe"),
                @"C:\Program Files\ExifToolGUI\exiftool.exe",
                @"C:\Program Files\ExifTool\exiftool.exe"
            };
            foreach (var path in paths) if (File.Exists(path)) return path;
            return "exiftool.exe";
        }

        void DisplayFile(string path)
        {
            currentFile = path;
            var m = metadata[path];
            fileTitle.Text = Path.GetFileName(path);
            cameraTitle.Text = JoinKnown(Value(m, "Make"), Value(m, "Model")) + "\n" +
                Value(m, "LensModel", "LensType") + "   ·   " + m.Count + " metadata tags";
            LoadPreview(path);

            FillTable(summary, new [] {
                Pair("Camera", JoinKnown(Value(m, "Make"), Value(m, "Model"))),
                Pair("Lens", Value(m, "LensModel", "LensType")),
                Pair("Captured", Value(m, "DateTimeOriginal", "CreateDate")),
                Pair("Shutter speed", Value(m, "ExposureTime", "ShutterSpeed")),
                Pair("Aperture", Value(m, "FNumber", "Aperture")),
                Pair("ISO", Value(m, "ISO")),
                Pair("Focal length", Value(m, "FocalLength")),
                Pair("Exposure compensation", Value(m, "ExposureCompensation")),
                Pair("White balance", Value(m, "WhiteBalance")),
                Pair("Photo style", Value(m, "PhotoStyle", "FilmMode"))
            });
            FillTable(lut, new [] {
                Pair("LUT 1 name", Value(m, "LUT1Name")),
                Pair("LUT 1 opacity", Value(m, "LUT1Opacity")),
                Pair("LUT 2 name", Value(m, "LUT2Name")),
                Pair("LUT 2 opacity", Value(m, "LUT2Opacity")),
                Pair("Photo style", Value(m, "PhotoStyle", "FilmMode")),
                Pair("Internal OutputLUT", Value(m, "OutputLUT") == "—" ? "Not present" : "Present")
            });
            FillAllGrid();
        }

        void LoadPreview(string path)
        {
            if (preview.Image != null) { var old = preview.Image; preview.Image = null; old.Dispose(); }
            try {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var image = Image.FromStream(stream))
                    preview.Image = new Bitmap(image);
            } catch { preview.Image = null; }
        }

        KeyValuePair<string,string> Pair(string key, string value)
        {
            return new KeyValuePair<string,string>(key, value);
        }

        void FillTable(DataGridView table, IEnumerable<KeyValuePair<string,string>> rows)
        {
            table.Rows.Clear();
            foreach (var item in rows) {
                table.Rows.Add(item.Key, item.Value);
            }
        }

        void FillAllGrid()
        {
            all.Rows.Clear();
            if (String.IsNullOrEmpty(currentFile)) return;
            string needle = search.Text.Trim();
            foreach (var item in metadata[currentFile].OrderBy(x => x.Key)) {
                string value = ConvertValue(item.Value);
                if (needle.Length == 0 || item.Key.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    all.Rows.Add(item.Key, value);
            }
        }

        string Value(Dictionary<string, object> m, params string[] names)
        {
            foreach (var name in names) {
                foreach (var item in m) {
                    if (item.Key.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                        item.Key.EndsWith(":" + name, StringComparison.OrdinalIgnoreCase))
                        return ConvertValue(item.Value);
                }
            }
            return "—";
        }

        string ConvertValue(object value)
        {
            if (value == null) return "—";
            var enumerable = value as IEnumerable;
            if (!(value is string) && enumerable != null) {
                var parts = new List<string>();
                foreach (var entry in enumerable) parts.Add(Convert.ToString(entry));
                return String.Join(", ", parts);
            }
            return Convert.ToString(value);
        }

        string JoinKnown(params string[] values)
        {
            var known = values.Where(x => !String.IsNullOrWhiteSpace(x) && x != "—").ToArray();
            return known.Length > 0 ? String.Join(" ", known) : "—";
        }

        FontFamily ResolveUiFont()
        {
            try {
                string bundledFonts = Path.Combine(runtimeDir, "fonts");
                if (Directory.Exists(bundledFonts)) {
                    foreach (var fontPath in Directory.GetFiles(bundledFonts, "*.ttf"))
                        privateFonts.AddFontFile(fontPath);
                    var bundledFamily = privateFonts.Families.FirstOrDefault(f =>
                        f.Name.Equals("Spoqa Han Sans Neo", StringComparison.OrdinalIgnoreCase));
                    if (bundledFamily != null) return bundledFamily;
                }
                using (var neo = new Font("Spoqa Han Sans Neo", 10))
                    if (String.Equals(neo.Name, "Spoqa Han Sans Neo", StringComparison.OrdinalIgnoreCase))
                        return new FontFamily("Spoqa Han Sans Neo");
                string userFonts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
                string[] candidates = {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SpoqaHanSansNeo-Regular.ttf"),
                    Path.Combine(userFonts, "Spoqa Han Sans Neo Regular.ttf"),
                    Path.Combine(userFonts, "Spoqa Han Sans Regular.ttf")
                };
                foreach (var candidate in candidates) {
                    if (!File.Exists(candidate)) continue;
                    privateFonts.AddFontFile(candidate);
                    if (privateFonts.Families.Length > 0) return privateFonts.Families[0];
                }
            } catch { }
            return new FontFamily("Segoe UI");
        }

        FontFamily ResolveThinFont()
        {
            try {
                string thinPath = Path.Combine(runtimeDir, "fonts", "SpoqaHanSansNeo-Thin.ttf");
                if (File.Exists(thinPath)) {
                    thinFonts.AddFontFile(thinPath);
                    if (thinFonts.Families.Length > 0) return thinFonts.Families[0];
                }
            } catch { }
            return uiFont;
        }

        string EnsureBundledRuntime()
        {
            string overridePath = Environment.GetEnvironmentVariable("EXIFIND_RUNTIME_DIR");
            string target = !String.IsNullOrWhiteSpace(overridePath) ? overridePath : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Exifind", "runtime-1.3");
            string marker = Path.Combine(target, ".ready");
            if (File.Exists(marker)) return target;

            Directory.CreateDirectory(target);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Exifind.Runtime.zip")) {
                if (stream == null) throw new InvalidOperationException("Bundled Exifind runtime was not found.");
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read)) {
                    foreach (var entry in archive.Entries) {
                        string destination = Path.GetFullPath(Path.Combine(target, entry.FullName));
                        if (!destination.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("Invalid bundled runtime path.");
                        if (String.IsNullOrEmpty(entry.Name)) {
                            Directory.CreateDirectory(destination);
                            continue;
                        }
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        entry.ExtractToFile(destination, true);
                    }
                }
            }
            File.WriteAllText(marker, "Exifind runtime 1.3");
            return target;
        }

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--probe") {
                try {
                    using (var form = new MainForm()) {
                        var result = form.ReadMetadata(args[1]);
                        Environment.Exit(result.Count > 0 ? 0 : 2);
                    }
                } catch {
                    Environment.Exit(3);
                }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
