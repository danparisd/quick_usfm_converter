﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using USFMToolsSharp;
using USFMToolsSharp.Models;
using USFMToolsSharp.Models.Markers;
using USFMToolsSharp.Renderers.Docx;
using USFMToolsSharp.Renderers.HTML;

namespace USFM_Converter
{
    public partial class MainForm : Form
    {
        private bool isTextJustified = false;
        private bool isSingleSpaced = true;
        private bool hasOneColumn = true;
        private bool isL2RDirection = true;
        private bool willSeparateChap = true;
        private bool willSeparateVerse = false;
        private string filePathConversion;


        private Dictionary<double, string> LineSpacingClasses;
        private string[] ColumnClasses;
        private string[] TextDirectionClasses;
        private string[] TextAlignmentClasses;
        private string fontClass;

        private Color whiteColor = Color.White;
        private Color darkBlue = Color.FromArgb(0, 68, 214);
        private Color disableBack = Color.FromArgb(215, 218, 224);
        private Color disableFore = Color.FromArgb(118, 118, 118);

        public MainForm()
        {
            InitCustomLabelFont();
            InitializeComponent();

            fileDataGrid.ColumnCount = 1;
            fileDataGrid.Columns[0].Name = "File";
            fileDataGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            LineSpacingClasses = new Dictionary<double, string>
            {
                [1] = "single-space",
                [1.5] = "one-half-space",
                [2] = "double-space",
                [2.5] = "two-half-space",
                [3] = "triple-space"
            };
            ColumnClasses = new string[]{ "", "two-column" };
            TextDirectionClasses = new string[] { "", "rtl-direct" };
            TextAlignmentClasses = new string[] { "", "right-align", "center-align", "justified" };

    }

        private void OnAddFilesButtonClick(object sender, EventArgs e)
        {


            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
            {
                Description = "Select the directory containing the files you want to convert.",
                // Default to the My Documents folder.
                RootFolder = Environment.SpecialFolder.MyComputer,
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal)
            };

            //Show the FolderBrowserDialog.
            DialogResult result = folderBrowserDialog.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }

            var folderName = folderBrowserDialog.SelectedPath;
            LoadFolder(folderName);

        }

        private void LoadFolder(string folderName)
        {
            List<string> supportedExtensions = new List<string> { ".usfm", ".txt", ".sfm" };
            var dirinfo = new DirectoryInfo(folderName);
            var allFiles = dirinfo.GetFiles("*", SearchOption.AllDirectories);
            foreach (FileInfo fileInfo in allFiles)
            {
                if (supportedExtensions.Contains(Path.GetExtension(fileInfo.FullName.ToLower())))
                {
                    fileDataGrid.Rows.Add(new String[] { fileInfo.FullName });
                }
            }

            Show_Conversion_Page();
            this.Btn_Convert.Enabled = true;
        }

        private void OnConvertButtonClick(object sender, EventArgs e)
        {
            //Implement for dropdown options setConfigObject();


            string saveFileName = (!string.IsNullOrWhiteSpace(FileNameInput.Text) ? FileNameInput.Text.Trim() : "out") + ".html";
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                FileName = saveFileName,
                Filter = "HTML files (*.html)|*.html|Word Document (*.docx)|*.docx|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = false
            };
            
            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
                
            }
            else
            {
                string fileName = saveFileDialog.FileName;
                btn_AddFiles.Enabled = false;
                fileDataGrid.Enabled = false;
                Show_Loading_Page();
                try
                {
                    if (Path.GetExtension(fileName) == ".html")
                    {
                        RenderHtml(fileName);
                    }
                    else if (Path.GetExtension(fileName) == ".docx")
                    {
                        RenderDocx(fileName);
                    }
                    btn_AddFiles.Enabled = true;
                    fileDataGrid.Enabled = true;
                    LoadingBar.Value = 0;
                    ResetValues();
                    Show_Success_Page();
                }
                catch(Exception ex)
                {
                    MessageBox.Show($"Error converting please submit a bug with a link to the USFM you're using and the following error message {ex.Message}", "Error converting", MessageBoxButtons.OK);
                    btn_AddFiles.Enabled = true;
                    fileDataGrid.Enabled = true;
                    LoadingBar.Value = 0;
                    ResetValues();
                    Show_Error_Page();
                }
            }
        }

        private void RenderHtml(string fileName)
        {
            // Does not parse through section headers yet
            var parser = new USFMParser(new List<string> { "s5" });

            HTMLConfig configHTML = BuildHTMLConfig();
            //Configure Settings -- Spacing ? 1, Column# ? 1, TextDirection ? L2R 
            var renderer = new HtmlRenderer(configHTML);

            // Added ULB License and Page Number
            renderer.FrontMatterHTML = GetLicenseInfo();
            renderer.InsertedFooter = GetFooterInfo();

            var usfm = new USFMDocument();

            var progress = fileDataGrid.RowCount - 1;
            var progressStep = 0;

            foreach (DataGridViewRow row in fileDataGrid.Rows)
            {
                var cell = row.Cells[0];
                if (cell.Value == null)
                {
                    continue;
                }
                var filename = cell.Value.ToString();

                var text = File.ReadAllText(filename);
                usfm.Insert(parser.ParseFromString(text));



                progressStep++;
                LoadingBar.Value = (int)(progressStep / (float)progress * 100);
            }

            var html = renderer.Render(usfm);

            File.WriteAllText(fileName, html);

            var dirname = Path.GetDirectoryName(fileName);
            filePathConversion = dirname;
            var cssFilename = Path.Combine(dirname, "style.css");
            if (!File.Exists(cssFilename))
            {
                File.Copy("style.css", cssFilename);
            }
        }

        private void RenderDocx(string fileName)
        {
            // Does not parse through section headers yet
            var parser = new USFMParser(new List<string> { "s5" });

            var renderer = new DocxRenderer(BuildDocxConfig());

            var usfm = new USFMDocument();

            var progress = fileDataGrid.RowCount - 1;
            var progressStep = 0;

            foreach (DataGridViewRow row in fileDataGrid.Rows)
            {
                var cell = row.Cells[0];
                if (cell.Value == null)
                {
                    continue;
                }
                var filename = cell.Value.ToString();

                var text = File.ReadAllText(filename);
                usfm.Insert(parser.ParseFromString(text));



                progressStep++;
                LoadingBar.Value = (int)(progressStep / (float)progress * 100);
            }

            var output = renderer.Render(usfm);

            using(Stream outputStream = File.Create(fileName))
            {
                output.Write(outputStream);
            }
        }

        private string GetLicenseInfo()
        {
            // Identifies License within Directory 
            string ULB_License_Doc = "insert_ULB_License.html";
            FileInfo f = new FileInfo(ULB_License_Doc);
            string licenseHTML = "";

            if (File.Exists(ULB_License_Doc))
            {

                licenseHTML = File.ReadAllText(ULB_License_Doc);
            }
            return licenseHTML;
        }
        private string GetFooterInfo()
        {
            // Format --  June 13, 2019 11:42
            string dateFormat = DateTime.Now.ToString("MM/dd/yyyy HH:mm");
            string footerHTML = $@"
            <div class=FooterSection>
            <table id='hrdftrtbl' border='0' cellspacing='0' cellpadding='0'>
            <div class=FooterSection>
            <table id='hrdftrtbl' border='0' cellspacing='0' cellpadding='0'>
            <tr><td>
            <div style='mso-element:footer' id=f1>
            <p class=MsoFooter></p>
            {dateFormat}
            <span style=mso-tab-count:1></span>
            <span style='mso-field-code: PAGE '></span><span style='mso-no-proof:yes'></span></span>
            <span style=mso-tab-count:1></span>
            <img alt='Creative Commons License' style='border-width:0' src='https://i.creativecommons.org/l/by-sa/4.0/88x31.png' />
            </p>
            </div>
            </td></tr>
            </table>
            </div> ";
            return footerHTML;
        }

        private void onAddOnlyFileClick(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "USFM files (*.usfm)|*.usfm|Text files (*.txt)|*.txt",
                Multiselect = true
            };
            
            //Show the FolderBrowserDialog.
            DialogResult result = openFileDialog.ShowDialog();
            if (result != DialogResult.OK)
            {
                return;
            }

            foreach (var filePath in openFileDialog.FileNames)
            {
                if (filePath.ToLower().EndsWith(".usfm") ||
                    filePath.ToLower().EndsWith(".txt"))
                {
                    fileDataGrid.Rows.Add(new String[] { filePath });
                }
            }
        }

        private void onRemoveFileButtonClick(object sender, EventArgs e)
        {
            DataGridViewSelectedCellCollection SelectedFiles = fileDataGrid.SelectedCells;
            int numRemove = 1;
            if (fileDataGrid.Rows.Count > 1)
            {
                foreach(DataGridViewCell SelectFile in SelectedFiles)
                {
                    if(SelectFile.OwningRow.Index != fileDataGrid.RowCount-1)
                        fileDataGrid.Rows.Remove(SelectFile.OwningRow);
                }
            }

            if (fileDataGrid.Rows.Count == 1)
            {
                numRemove = 0;
            }
            btn_Remove.Text = $"Delete ({numRemove}) Files";
        }

        private void Btn_NewProj_Click(object sender, EventArgs e)
        {
            Show_Home_Page();
            fileDataGrid.Rows.Clear();
        }

        private void Btn_OpenFileLocation_Click(object sender, EventArgs e)
        {
            if (filePathConversion == null)
                Process.Start("explorer.exe");
            else
                Process.Start(filePathConversion);
        }
        private void fileDataGrid_CellStateChanged(object sender, DataGridViewCellStateChangedEventArgs e)
        {
            DataGridViewElementStates state = e.StateChanged;
            DataGridViewSelectedCellCollection SelectedFiles = fileDataGrid.SelectedCells;
            int numFilesRemove = SelectedFiles.Count;
            if (SelectedFiles.Contains(fileDataGrid[0, fileDataGrid.RowCount-1]))
                numFilesRemove--;

            btn_Remove.Text = $"Delete ({numFilesRemove}) Files";
        }

        private void Btn_Format_Click(object sender, EventArgs e)
        {
            Show_Format_Page();
        }

        private void Btn_Spaced_Click(object sender, EventArgs e)
        {
            // Pseudo Radio Button Styling
            isSingleSpaced = !isSingleSpaced;
            if (isSingleSpaced)
            {
                setColorFocus(this.Btn_DoubleSpaced, false);
                setColorFocus(this.Btn_SingleSpaced, true);
            }
            else
            {
                setColorFocus(this.Btn_DoubleSpaced, true);
                setColorFocus(this.Btn_SingleSpaced, false);
            }
        }

        private void Btn_Col_Click(object sender, EventArgs e)
        {
            hasOneColumn = !hasOneColumn;
            if (hasOneColumn)
            {
                setColorFocus(this.Btn_TwoCol, false);
                setColorFocus(this.Btn_OneCol, true);
            }
            else
            {
                setColorFocus(this.Btn_TwoCol, true);
                setColorFocus(this.Btn_OneCol, false);
            }
        }

        private void Btn_Direction_Click(object sender, EventArgs e)
        {
            ComponentResourceManager resources = new ComponentResourceManager(typeof(MainForm));
            isL2RDirection = !isL2RDirection;
            if (isL2RDirection)
            {
                setColorFocus(this.Btn_LTR, true);
                setColorFocus(this.Btn_RTL, false);

                // Switch Alignment
                this.Btn_TextAlignDefault.Text = "   Left Aligned";
                this.Btn_TextAlignDefault.Image = Properties.Resources.Text_Align;
                this.Btn_TextAlignDefault.TextImageRelation = TextImageRelation.ImageBeforeText;

            }
            else
            {
                setColorFocus(this.Btn_LTR, false);
                setColorFocus(this.Btn_RTL, true);

                this.Btn_TextAlignDefault.Text = "   Right Aligned";
                this.Btn_TextAlignDefault.Image = Properties.Resources.Text_Align_R;
                this.Btn_TextAlignDefault.TextImageRelation = TextImageRelation.ImageBeforeText;

            }
        }
        private void Btn_TextAlign_Click(object sender, EventArgs e)
        {
            isTextJustified = !isTextJustified;
            if (isTextJustified)
            {
                setColorFocus(this.Btn_TextAlignDefault, false);
                setColorFocus(this.Btn_TextJustify, true);
            }
            else
            {
                setColorFocus(this.Btn_TextAlignDefault, true);
                setColorFocus(this.Btn_TextJustify, false);
            }

        }
        private void Btn_Chap_Click(object sender, EventArgs e)
        {
            willSeparateChap = !willSeparateChap;
            if (willSeparateChap)
            {
                setColorFocus(this.Btn_ChapComb, false);
                setColorFocus(this.Btn_ChapBreak, true);
            }
            else
            {
                setColorFocus(this.Btn_ChapComb, true);
                setColorFocus(this.Btn_ChapBreak, false);
            }
        }
        private void Btn_VerseDefault_Click(object sender, EventArgs e)
        {
            willSeparateVerse = !willSeparateVerse;
            if (willSeparateVerse)
            {
                setColorFocus(this.Btn_VerseDefault, false);
                setColorFocus(this.Btn_SeparateVerse, true);
            }
            else
            {
                setColorFocus(this.Btn_VerseDefault, true);
                setColorFocus(this.Btn_SeparateVerse, false);
            }
        }
        private void Btn_FormatBack_Click(object sender, EventArgs e)
        {
            Show_Conversion_Page();
        }

        private void Show_Home_Page()
        {
            Success_Page.Visible = false;
            Conversion_Page.Visible = false;
            Loading_Page.Visible = false;
            Format_Page.Visible = false;
            Error_Page.Visible = false;
            HomeCapture.Visible = true;
        }
        private void Show_Conversion_Page()
        {
            Success_Page.Visible = false;
            Conversion_Page.Visible = true;
            Loading_Page.Visible = false;
            Format_Page.Visible = false;
            Error_Page.Visible = false;
            HomeCapture.Visible = false;
        }
        private void Show_Success_Page()
        {
            Success_Page.Visible = true;
            Conversion_Page.Visible = false;
            Loading_Page.Visible = false;
            Format_Page.Visible = false;
            Error_Page.Visible = false;
            HomeCapture.Visible = false;
        }
        private void Show_Format_Page()
        {
            Success_Page.Visible = false;
            Conversion_Page.Visible = false;
            Loading_Page.Visible = false;
            Format_Page.Visible = true;
            Error_Page.Visible = false;
            HomeCapture.Visible = false;
        }
        private void Show_Error_Page()
        {
            Success_Page.Visible = false;
            Conversion_Page.Visible = false;
            Loading_Page.Visible = false;
            Format_Page.Visible = false;
            Error_Page.Visible = true;
        }
        private void Show_Loading_Page()
        {
            Success_Page.Visible = false;
            Conversion_Page.Visible = false;
            Loading_Page.Visible = true;
            Format_Page.Visible = false;
            Error_Page.Visible = false;
        }
        private void ResetValues()
        {
            FileNameInput.Text = "";

        }        
        private HTMLConfig BuildHTMLConfig()
        {
            HTMLConfig config = new HTMLConfig();
            if (!isSingleSpaced)
            {
                config.divClasses.Add(LineSpacingClasses[2.0]);
            }
            if (!hasOneColumn)
            {
                config.divClasses.Add(ColumnClasses[1]);
            }
            if (!isL2RDirection)
            {
                config.divClasses.Add(TextDirectionClasses[1]);
            }
            if (isTextJustified)
            {
                config.divClasses.Add(TextAlignmentClasses[3]);
            }
            config.divClasses.Add(fontClass);

            // Will be added to HTML config class 
            config.separateVerses = willSeparateVerse;

            config.separateChapters = willSeparateChap;

            return config;
        }
        private DocxConfig BuildDocxConfig()
        {
            DocxConfig config = new DocxConfig();
            config.lineSpacing = isSingleSpaced ? 1: 2;
            config.columnCount = hasOneColumn ? 1 : 2;
            config.rightToLeft = !isL2RDirection;
            config.separateVerses = willSeparateVerse;
            config.separateChapters = willSeparateChap;

            return config;
        }
        private void setColorFocus(Button sender,bool focus)
        {
            if (focus)
            {
                sender.BackColor = whiteColor;
                sender.FlatAppearance.BorderColor = darkBlue;
                sender.ForeColor = darkBlue;
            }
            else
            {
                // Grayed Out Button
                sender.BackColor = disableBack;
                sender.FlatAppearance.BorderColor = disableFore;
                sender.ForeColor = disableFore;
            }
        }

        private void Btn_FontSmall_Click(object sender, EventArgs e)
        {
            setColorFocus(this.Btn_FontSmall, true);
            setColorFocus(this.Btn_FontMed, false);
            setColorFocus(this.Btn_FontLarge, false);
            fontClass = "small-text";
        }

        private void Btn_FontMed_Click(object sender, EventArgs e)
        {
            setColorFocus(this.Btn_FontSmall, false);
            setColorFocus(this.Btn_FontMed, true);
            setColorFocus(this.Btn_FontLarge, false);
            fontClass = "med-text";
        }

        private void Btn_FontLarge_Click(object sender, EventArgs e)
        {
            setColorFocus(this.Btn_FontSmall, false);
            setColorFocus(this.Btn_FontMed, false);
            setColorFocus(this.Btn_FontLarge, true);
            fontClass = "large-text";
        }

        private void HomeCapture_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length == 1)
                {
                    var fileAttributes = File.GetAttributes(files[0]);
                    if (fileAttributes.HasFlag(FileAttributes.Directory))
                    {
                        e.Effect = DragDropEffects.Copy;
                    }
                }
            }
        }

        private void HomeCapture_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length == 1)
                {
                    var fileAttributes = File.GetAttributes(files[0]);
                    if (fileAttributes.HasFlag(FileAttributes.Directory))
                    {
                        LoadFolder(files[0]);
                    }
                }
            }
        }
    }

}
