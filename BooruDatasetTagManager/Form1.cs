﻿using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Translator;
using static BooruDatasetTagManager.DatasetManager;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace BooruDatasetTagManager
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            tagsBuffer = new List<string>();
            gridViewTags.CellValueChanged += DataGridView1_CellValueChanged;
            gridViewTags.RowsAdded += DataGridView1_RowsAdded;
            gridViewTags.RowsRemoved += DataGridView1_RowsRemoved;
            previewPicBox = new PictureBox();
            previewPicBox.Name = "previewPicBox";
        }

        private void DataGridView1_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            SetChangedStatus(true);
        }

        private void DataGridView1_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            SetChangedStatus(true);
        }

        private void DataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            SetChangedStatus(true);
        }

        List<string> tagsBuffer;

        private bool isAllTags = true;
        private bool isTranslate = false;
        private bool isFiltered = false;

        private Form_preview fPreview;
        private bool isShowPreview = false;
        private PictureBox previewPicBox;
        private int previewRowIndex = -1;
        private bool filterAnd = false;
        private int lastGridViewTagsHash = -1;
        private bool isLoading = false;


        Dictionary<string, string> Trans = new Dictionary<string, string>();
        string TransPath => "trans_" + Program.Settings.TranslationLanguage;
        string lastTransFile = null;

        private void Form1_Load(object sender, EventArgs e)
        {
            Text += " " + Application.ProductVersion;
            gridViewDS.RowTemplate.Height = Program.Settings.PreviewSize + 10;
            splitContainer2.SplitterDistance = Width / 3;
            LoadTrans();
        }

        private void LoadTrans()
        {
            Trans.Clear();
            if (File.Exists(TransPath))
            {
                lastTransFile = TransPath;
                string[] list = File.ReadAllLines(TransPath);
                foreach (var item in list)
                {
                    string[] kv = item.Split('-');
                    if (kv.Length == 2)
                    {
                        if (!Trans.ContainsKey(kv[0]))
                        {
                            Trans.Add(kv[0], kv[1]);
                        }
                    }
                }
            }
        }

        private void SetChangedStatus(bool changed)
        {
            toolStripButton1.Enabled = changed;
            toolStripButton11.Enabled = changed;
        }

        private void openFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFolderDialog openFolderDialog = new OpenFolderDialog();
            if (openFolderDialog.ShowDialog() != DialogResult.OK)
                return;
            isLoading = true;
            Program.DataManager = new DatasetManager();
            Program.DataManager.LoadFromFolder(openFolderDialog.Folder);

            gridViewDS.DataSource = Program.DataManager.GetDataSource();
            Program.DataManager.UpdateData();
            BindTagList();
            ApplyDataSetGridStyle();
            isLoading = false;
        }

        private async Task FillTranslation(DataGridView grid)
        {
            LockEdit(true);
            SetStatus("Translating, please wait...");
            string transLang = Program.Settings.TranslationLanguage;
            try
            {
                HttpClient client = new HttpClient();
                for (int i = 0; i < grid.RowCount; i++)
                {
                    SetStatus($"Translation {i}/{grid.RowCount}");
                    grid["Translation", i].ReadOnly = true;
                    grid["Translation", i].Value = await Translation(client, transLang, grid[0, i].Value as string);
                }
            }
            catch (Exception ex)
            {

            }
            SetStatus("Translation completed");
            LockEdit(false);
        }

        private async Task<string> Translation(HttpClient client, string transLang, string transString)
        {
            if (string.IsNullOrWhiteSpace(transString))
                return null;
            string key = transString.ToLower();
            if(lastTransFile != TransPath)
            {
                LoadTrans();
            }
            if (Trans.TryGetValue(key, out string value))
            {
                return value;
            }
            transLang = transLang.Replace("-CN", "").Replace("-TW", "");
            FormUrlEncodedContent content = new FormUrlEncodedContent(new KeyValuePair<string, string>[]
                   {
                       new KeyValuePair<string, string>("appid","105"),
                       new KeyValuePair<string, string>("sgid","auto"),
                       new KeyValuePair<string, string>("sbid","auto"),
                       new KeyValuePair<string, string>("egid",transLang),
                       new KeyValuePair<string, string>("ebid",transLang),
                       new KeyValuePair<string, string>("content",transString.Replace('-',' ').Replace('_',' ')),
                       new KeyValuePair<string, string>("type","2"),
                   });
            var ret = await client.PostAsync($"https://translate-api-fykz.xiangtatech.com/translation/webs/index", content);
            if (ret.IsSuccessStatusCode)
            {
                string json = await ret.Content.ReadAsStringAsync();
                int begin = json.IndexOf("\"by\":\"");
                if (begin == -1)
                    return null;
                begin += 6;
                int end = json.IndexOf("\"", begin);
                if (end == -1)
                    return null;
                value = json.Substring(begin, end - begin);
                Trans[key] = value;
                File.AppendAllText(TransPath, $"{key}-{value}\r\n", Encoding.UTF8);
                return value;
            }
            return null;
        }

        private void LockEdit(bool locked)
        {
            toolStrip2.Enabled = !locked;
            toolStrip1.Enabled = !locked;
            gridViewTags.Enabled = !locked;
            if (gridViewTags.SelectedRows.Count == 1)
                gridViewTags.AllowDrop = !locked;
            gridViewAllTags.Enabled = !locked;
            gridViewDS.Enabled = !locked;
        }

        private void ShowPreview(string img)
        {
            if (fPreview == null || fPreview.IsDisposed)
                fPreview = new Form_preview();
            fPreview.Show(img);
        }

        private void HidePreview()
        {
            fPreview?.Hide();
        }

        private async void LoadSelectedImageToGrid()
        {
            if (gridViewDS.SelectedRows.Count == 0)
                return;
            if (gridViewDS.SelectedRows.Count == 1)
            {
                gridViewTags.AllowDrop = true;
                gridViewTags.Rows.Clear();
                ChageImageColumn(false);
                List<string> tags = Program.DataManager.DataSet[(string)gridViewDS.SelectedRows[0].Cells["ImageFilePath"].Value].Tags;
                gridViewTags.Tag = (string)gridViewDS.SelectedRows[0].Cells["ImageFilePath"].Value;
                gridViewTags.Columns["ImageTags"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                if (gridViewTags.Columns.Contains("Translation"))
                {
                    gridViewTags.Columns["Translation"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                    gridViewTags.Columns["Translation"].ReadOnly = true;
                }
                foreach (var item in tags)
                    gridViewTags.Rows.Add(item);
                if (isShowPreview)
                {
                    ShowPreview((string)gridViewDS.SelectedRows[0].Cells["ImageFilePath"].Value);
                }
            }
            else
            {
                if (isShowPreview)
                {
                    HidePreview();
                }
                gridViewTags.AllowDrop = false;
                gridViewTags.Rows.Clear();
                ChageImageColumn(true);
                gridViewTags.Columns["ImageTags"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader;
                if (gridViewTags.Columns.Contains("Translation"))
                {
                    gridViewTags.Columns["Translation"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader;
                    gridViewTags.Columns["Translation"].ReadOnly = true;
                }
                gridViewTags.Tag = "0";
                Dictionary<string, List<DataItem>> table = new Dictionary<string, List<DataItem>>();
                List<DataItem> selectedTagsList = new List<DataItem>();
                for (int i = 0; i < gridViewDS.SelectedRows.Count; i++)
                {
                    selectedTagsList.Add(Program.DataManager.DataSet[(string)gridViewDS.SelectedRows[i].Cells["ImageFilePath"].Value]);
                }

                int maxCount = selectedTagsList.Max(a => a.Tags.Count);

                for (int i = 0; i < maxCount; i++)
                {
                    for (int j = 0; j < selectedTagsList.Count; j++)
                    {
                        var curTags = selectedTagsList[j];
                        if (i < curTags.Tags.Count)
                        {
                            if (table.ContainsKey(curTags.Tags[i]))
                            {
                                table[curTags.Tags[i]].Add(curTags);
                            }
                            else
                            {
                                table.Add(curTags.Tags[i], new List<DataItem>() { curTags });
                            }
                        }
                    }
                }
                foreach (var item in table)
                {
                    item.Value.Sort((x, y) => x.Name.CompareTo(y.Name));
                    for (int i = 0; i < item.Value.Count; i++)
                    {
                        int rowIndex = gridViewTags.Rows.Add();
                        DataGridViewRow row = gridViewTags.Rows[rowIndex];
                        row.Tag = item.Key;
                        row.Cells["ImageTags"].Value = i == 0 ? item.Key : "";
                        row.Cells["ImageTags"].Tag = item.Value[i];
                        row.Cells["Image"].Value = item.Value[i].ImageFilePath;
                        row.Cells["Image"].Tag = item.Key;
                        row.Cells["Name"].Value = item.Value[i].Name;
                        row.Cells["Image"].Tag = item.Key;
                    }
                }
            }
            gridViewDS.Focus();
            if (isTranslate)
                await FillTranslation(gridViewTags);
            SetChangedStatus(false);
        }

        /// <summary>
        /// Add or remove Image column
        /// </summary>
        /// <param name="add"> true to add, false to remove</param>
        private void ChageImageColumn(bool add)
        {
            if (gridViewTags.Columns.Contains("Image"))
            {
                if (!add)
                {
                    gridViewTags.Columns.Remove("Image");
                    gridViewTags.Columns.Remove("Name");
                }
            }
            else
            {
                if (add)
                {
                    gridViewTags.Columns.Add("Image", "Image");
                    gridViewTags.Columns["Image"].Visible = false;
                    gridViewTags.Columns.Add("Name", "Name");
                    gridViewTags.Columns["Name"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader;
                }
            }
        }

        private Rectangle dragBoxFromMouseDown;
        private int rowIndexFromMouseDown;
        private int rowIndexOfItemUnderMouseToDrop;
        private void dataGridView1_MouseMove(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                // If the mouse moves outside the rectangle, start the drag.
                if (dragBoxFromMouseDown != Rectangle.Empty &&
                    !dragBoxFromMouseDown.Contains(e.X, e.Y))
                {

                    // Proceed with the drag and drop, passing in the list item.                    
                    DragDropEffects dropEffect = gridViewTags.DoDragDrop(
                    gridViewTags.Rows[rowIndexFromMouseDown],
                    DragDropEffects.Move);
                }
            }
        }

        private void dataGridView1_MouseDown(object sender, MouseEventArgs e)
        {
            // Get the index of the item the mouse is below.
            rowIndexFromMouseDown = gridViewTags.HitTest(e.X, e.Y).RowIndex;
            if (rowIndexFromMouseDown != -1)
            {
                // Remember the point where the mouse down occurred. 
                // The DragSize indicates the size that the mouse can move 
                // before a drag event should be started.                
                Size dragSize = SystemInformation.DragSize;

                // Create a rectangle using the DragSize, with the mouse position being
                // at the center of the rectangle.
                dragBoxFromMouseDown = new Rectangle(new Point(e.X - (dragSize.Width / 2),
                                                               e.Y - (dragSize.Height / 2)),
                                    dragSize);
            }
            else
                // Reset the rectangle if the mouse is not over an item in the ListBox.
                dragBoxFromMouseDown = Rectangle.Empty;
        }

        private void dataGridView1_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void dataGridView1_DragDrop(object sender, DragEventArgs e)
        {
            // The mouse locations are relative to the screen, so they must be 
            // converted to client coordinates.
            Point clientPoint = gridViewTags.PointToClient(new Point(e.X, e.Y));

            // Get the row index of the item the mouse is below. 
            rowIndexOfItemUnderMouseToDrop =
                gridViewTags.HitTest(clientPoint.X, clientPoint.Y).RowIndex;

            // If the drag operation was a move then remove and insert the row.
            if (e.Effect == DragDropEffects.Move)
            {
                if (rowIndexFromMouseDown != rowIndexOfItemUnderMouseToDrop)
                {
                    DataGridViewRow rowToMove = e.Data.GetData(
                        typeof(DataGridViewRow)) as DataGridViewRow;
                    gridViewTags.Rows.RemoveAt(rowIndexFromMouseDown);
                    gridViewTags.Rows.Insert(rowIndexOfItemUnderMouseToDrop, rowToMove);
                    gridViewTags.ClearSelection();
                    gridViewTags[0, rowIndexOfItemUnderMouseToDrop].Selected = true;
                }
            }
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            AddNewRow();
        }

        private void AddNewRow()
        {
            if (gridViewDS.SelectedRows.Count > 1)
            {
                //MessageBox.Show("Adding tags does not support multiple selection. Choose one image.");
                //return;
                using (Form_addTag addTag = new Form_addTag())
                {
                    addTag.comboBox1.Enabled = false;
                    if (addTag.ShowDialog() == DialogResult.OK)
                    {
                        AddTagMultiselectedMode(addTag.textBox1.Text);
                    }
                    addTag.Close();
                }
            }
            else
            {
                if (gridViewTags.SelectedCells.Count == 0 || gridViewTags.RowCount == 0)
                    gridViewTags.Rows.Add();
                else
                {
                    gridViewTags.Rows.Insert(gridViewTags.SelectedCells[0].RowIndex + 1);
                }
            }
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            if (gridViewTags.SelectedCells.Count == 0)
                return;
            gridViewTags.Rows.RemoveAt(gridViewTags.SelectedCells[0].RowIndex);
        }

        private void toolStripButton4_Click(object sender, EventArgs e)
        {
            if (gridViewTags.SelectedCells.Count == 0 || gridViewTags.SelectedCells[0].RowIndex == 0)
                return;
            int curIndex = gridViewTags.SelectedCells[0].RowIndex;
            string upperValue = (string)gridViewTags[0, curIndex - 1].Value;
            if (isTranslate)
            {
                string upperValueTrans = (string)gridViewTags[1, curIndex - 1].Value;
                gridViewTags[1, curIndex - 1].Value = gridViewTags[1, curIndex].Value;
                gridViewTags[1, curIndex].Value = upperValueTrans;
            }
            gridViewTags[0, curIndex - 1].Value = gridViewTags[0, curIndex].Value;
            gridViewTags[0, curIndex].Value = upperValue;
            gridViewTags.ClearSelection();
            gridViewTags[0, curIndex - 1].Selected = true;
        }

        private void toolStripButton5_Click(object sender, EventArgs e)
        {
            if (gridViewTags.SelectedCells.Count == 0 || gridViewTags.SelectedCells[0].RowIndex == gridViewTags.RowCount - 1)
                return;
            int curIndex = gridViewTags.SelectedCells[0].RowIndex;
            string lowerValue = (string)gridViewTags[0, curIndex + 1].Value;

            if (isTranslate)
            {
                string lowerValueTrans = (string)gridViewTags[1, curIndex + 1].Value;
                gridViewTags[1, curIndex + 1].Value = gridViewTags[1, curIndex].Value;
                gridViewTags[1, curIndex].Value = lowerValueTrans;
            }

            gridViewTags[0, curIndex + 1].Value = gridViewTags[0, curIndex].Value;
            gridViewTags[0, curIndex].Value = lowerValue;
            gridViewTags.ClearSelection();
            gridViewTags[0, curIndex + 1].Selected = true;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            ApplyTagsChanges();
        }

        private void ApplyTagsChanges()
        {
            if (Program.DataManager == null)
            {
                MessageBox.Show("Dataset not load.");
                return;
            }
            if ((string)gridViewTags.Tag != "0")
            {
                List<string> nTags = new List<string>();
                for (int i = 0; i < gridViewTags.RowCount; i++)
                {
                    nTags.Add((string)gridViewTags[0, i].Value);
                }
                Program.DataManager.DataSet[(string)gridViewTags.Tag].Tags = nTags;
            }
            else
            {
                Dictionary<string, List<string>> nTagsList = new Dictionary<string, List<string>>();
                for (int i = 0; i < gridViewTags.RowCount; i++)
                {
                    string tag = (string)gridViewTags["Image", i].Tag;
                    string img = (string)gridViewTags["Image", i].Value;
                    if (string.IsNullOrEmpty(img))
                        throw new Exception("Image file name is empty!");
                    if (string.IsNullOrEmpty(tag) && !string.IsNullOrEmpty((string)gridViewTags["ImageTags", i].Value))
                        throw new NotImplementedException();
                    if (string.IsNullOrWhiteSpace(tag))
                        continue;
                    if (nTagsList.ContainsKey(img))
                        nTagsList[img].Add(tag);
                    else
                        nTagsList.Add(img, new List<string>() { tag });
                }
                foreach (var item in nTagsList)
                {
                    Program.DataManager.DataSet[item.Key].Tags = item.Value;
                }
            }
            Program.DataManager.UpdateData();
            BindTagList();
            SetChangedStatus(false);
            lastGridViewTagsHash = GetgridViewTagsHash();
            SetStatus("Saved");
        }

        private void toolStripButton6_Click(object sender, EventArgs e)
        {
            isAllTags = !isAllTags;
            if (isAllTags)
                label3.Text = "All tags";
            else
                label3.Text = "Common tags";
            BindTagList();
        }

        private void BindTagList()
        {
            if (Program.DataManager == null)
            {
                MessageBox.Show("Dataset not load.");
                return;
            }
            if (isAllTags)
            {
                BingSourceToDGV(gridViewAllTags, Program.DataManager.AllTags);
            }
            else
            {
                BingSourceToDGV(gridViewAllTags, Program.DataManager.CommonTags);
            }
            gridViewAllTags.Columns["Tag"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        }

        private async void BingSourceToDGV(DataGridView dgv, List<TagValue> source)
        {
            var scroll = dgv.FirstDisplayedScrollingRowIndex;
            var all = GetSelectedTags();
            dgv.Rows.Clear();
            if (dgv.Columns.Count == 0)
                dgv.Columns.Add("Tag", "Tag");
            foreach (var item in source)
            {
                int row = dgv.Rows.Add(item.Tag);
                if (row == 0)
                    dgv.Rows[row].Selected = false;
                if (all.Contains(item.Tag))
                    dgv.Rows[row].Selected = true;
            }
            
            if(scroll >= dgv.RowCount)
            {
                scroll = dgv.Rows.Count - 1;
            }
            if (scroll != -1)
            {
                dgv.FirstDisplayedScrollingRowIndex = scroll;
            }
            if (isTranslate)
            {
                await FillTranslation(dgv);
            }
        }

        private void toolStripButton7_Click(object sender, EventArgs e)
        {
            AddTagToAll(false);
        }

        private async void AddTagToAll(bool filtered)
        {
            Form_addTag addTag = new Form_addTag();
            int index = gridViewAllTags.RowCount;
            if (gridViewAllTags.SelectedCells.Count > 0)
            {
                index = gridViewAllTags.SelectedCells[0].RowIndex;
                addTag.textBox1.Text = (string)gridViewAllTags.Rows[index].Cells[0].Value;
                addTag.textBox1.SelectAll();
            }
            if (addTag.ShowDialog() == DialogResult.OK)
            {
                int customIndex = (int)addTag.numericUpDown1.Value;

                DatasetManager.AddingType addType = (DatasetManager.AddingType)Enum.Parse(typeof(DatasetManager.AddingType), (string)addTag.comboBox1.SelectedItem);
                Program.DataManager.AddTagToAll(addTag.textBox1.Text, addType, customIndex, filtered);
                Program.DataManager.UpdateData();
                int valIndex = IndexOfValueInGrig(gridViewTags, "ImageTags", addTag.textBox1.Text);
                if (gridViewDS.SelectedRows.Count == 1)
                {
                    if (valIndex != -1)
                    {
                        gridViewTags.Rows.RemoveAt(valIndex);
                    }
                    int insertIndex = 0;
                    switch (addType)
                    {
                        case DatasetManager.AddingType.Top:
                            {
                                insertIndex = 0;
                                break;
                            }
                        case DatasetManager.AddingType.Center:
                            {
                                insertIndex = gridViewTags.RowCount / 2;
                                break;
                            }
                        case DatasetManager.AddingType.Down:
                            {
                                insertIndex = gridViewTags.RowCount;
                                break;
                            }
                        case DatasetManager.AddingType.Custom:
                            {
                                if (customIndex >= gridViewTags.RowCount)
                                {
                                    insertIndex = gridViewTags.RowCount;
                                }
                                else if (customIndex < 0)
                                {
                                    insertIndex = 0;
                                }
                                else
                                    insertIndex = customIndex;
                                break;
                            }
                    }
                    gridViewTags.Rows.Insert(insertIndex, addTag.textBox1.Text);
                    string transString = null;
                    if (isTranslate)
                    {
                        transString = await Translation(new HttpClient(), Program.Settings.TranslationLanguage, addTag.textBox1.Text);
                        gridViewTags.Rows[insertIndex].Cells[1].Value = transString;
                    }

                    var allIndex = IndexOfValueInGrig(gridViewAllTags, "Tag", addTag.textBox1.Text);
                    if (allIndex == -1)
                    {
                        gridViewAllTags.Rows.Insert(index, 1);
                        gridViewAllTags.Rows[index].Cells[0].Value = addTag.textBox1.Text;
                        if (isTranslate)
                        {
                            gridViewAllTags.Rows[index].Cells[1].Value = transString;
                        }
                    }
                }
                else
                {
                    AddTagMultiselectedMode(addTag.textBox1.Text);
                }
                //BindTagList();
            }
            addTag.Close();
        }

        private int IndexOfValueInGrig(DataGridView gridView, string colName, string value)
        {
            for (int i = 0; i < gridView.RowCount; i++)
            {
                if (gridView[colName, i].Value != DBNull.Value)
                {
                    if ((string)gridView[colName, i].Value == value)
                        return i;
                }
                else if (value == null)
                    return i;
            }
            return -1;
        }

        private void toolStripButton8_Click(object sender, EventArgs e)
        {
            if (gridViewDS.SelectedRows.Count != 1)
            {
                MessageBox.Show("Replace does not support multiple selection. Choose one image.");
                return;
            }

            if (gridViewAllTags.SelectedCells.Count == 0)
                return;
            Form_replaceAll replaceAll = new Form_replaceAll();
            replaceAll.comboBox1.DataSource = Program.DataManager.AllTags;
            replaceAll.comboBox1.DisplayMember = "Tag";
            replaceAll.comboBox1.SelectedIndex = gridViewAllTags.SelectedCells[0].RowIndex;
            replaceAll.comboBox2.Items.AddRange(Program.DataManager.AllTags.Select(a => a.Tag).ToArray());
            if (replaceAll.ShowDialog() == DialogResult.OK)
            {
                Program.DataManager.ReplaceTagInAll(((TagValue)replaceAll.comboBox1.SelectedItem).Tag, (string)replaceAll.comboBox2.Text, true);
                Program.DataManager.UpdateData();
                int indexToReplace = -1;
                int indexToDelete = -1;
                for (int i = 0; i < gridViewTags.RowCount; i++)
                {
                    string srcText = (string)gridViewTags[0, i].Value;
                    if (srcText == (string)replaceAll.comboBox2.Text)
                        indexToDelete = i;
                    else if (srcText == ((TagValue)replaceAll.comboBox1.SelectedItem).Tag)
                        indexToReplace = i;
                }
                if (indexToReplace != -1)
                {
                    gridViewTags[0, indexToReplace].Value = (string)replaceAll.comboBox2.Text;
                    if (indexToDelete != -1)
                        gridViewTags.Rows.RemoveAt(indexToDelete);
                }
            }
            replaceAll.Close();
            BindTagList();
        }

        private void saveAllChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Program.DataManager == null)
            {
                MessageBox.Show("Dataset not load.");
                return;
            }
            ApplyTagsChanges();
            Program.DataManager.SaveAll();
            Program.DataManager.UpdateDatasetHash();
            SetStatus("Saved!");
            MessageBox.Show("Saved!");
        }

        private void showPreviewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Program.DataManager == null)
            {
                MessageBox.Show("Dataset not load.");
                return;
            }
            isShowPreview = !isShowPreview;
            showPreviewToolStripMenuItem.Checked = isShowPreview;
            if (isShowPreview)
            {
                if (gridViewDS.SelectedRows.Count == 1)
                {
                    ShowPreview((string)gridViewDS.SelectedRows[0].Cells["ImageFilePath"].Value);
                }
                else
                {
                    HidePreview();
                }
            }
            else
            {
                HidePreview();
            }
        }

        private void toolStripButton9_Click(object sender, EventArgs e)
        {
            if (gridViewDS.SelectedRows.Count == 1)
            {
                tagsBuffer.Clear();
                for (int i = 0; i < gridViewTags.RowCount; i++)
                {
                    tagsBuffer.Add((string)gridViewTags[0, i].Value);
                }
                SetStatus("Copied!");
            }
            else if (gridViewDS.SelectedRows.Count > 1)
            {
                MessageBox.Show("Copying is only supported for single selection");
            }
            else
            {
                MessageBox.Show("First select an image");
            }
        }

        private void SetStatus(string text)
        {
            statusLabel.Text = text;
        }

        private async void toolStripButton10_Click(object sender, EventArgs e)
        {
            if (gridViewDS.SelectedRows.Count == 1)
            {
                gridViewTags.Rows.Clear();
                for (int i = 0; i < tagsBuffer.Count; i++)
                {
                    gridViewTags.Rows.Add(tagsBuffer[i]);
                }
                if (isTranslate)
                    await FillTranslation(gridViewTags);
                SetStatus("Pasted!");
            }
            else if (gridViewDS.SelectedRows.Count > 1)
            {
                MessageBox.Show("Pasting is only supported for single selection");
            }
            else
            {
                MessageBox.Show("First select an image");
            }
        }

        private void toolStripButton11_Click(object sender, EventArgs e)
        {
            LoadSelectedImageToGrid();
            lastGridViewTagsHash = GetgridViewTagsHash();
        }

        private void toolStripButton12_Click(object sender, EventArgs e)
        {
            RemoveTagFromAll(false);
        }

        private void RemoveTagFromAll(bool filtered)
        {

            List<KeyValuePair<int, string>> tagsToDel = new List<KeyValuePair<int, string>>();
            for (int i = 0; i < gridViewAllTags.SelectedCells.Count; i++)
            {
                var row = gridViewAllTags.SelectedCells[i].RowIndex;
                tagsToDel.Add(new KeyValuePair<int, string>(row, (string)gridViewAllTags.Rows[row].Cells[0].Value));
            }

            tagsToDel.Sort((a, b) => b.Key.CompareTo(a.Key));

            foreach (var item in tagsToDel)
            {
                Program.DataManager.DeleteTagFromAll(item.Value, filtered);
                RemoveTagFromImageTags(item.Value);
                gridViewAllTags.Rows.RemoveAt(item.Key);
            }
            Program.DataManager.UpdateData();
        }

        private async void translateTagsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            isTranslate = !isTranslate;
            translateTagsToolStripMenuItem.Checked = isTranslate;
            if (isTranslate)
            {
                gridViewAllTags.Columns.Insert(1, new DataGridViewTextBoxColumn()
                {
                    Name = "Translation",
                    HeaderText = "Translation",
                    ReadOnly = true,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                });
                gridViewTags.Columns.Insert(1, new DataGridViewTextBoxColumn()
                {
                    Name = "Translation",
                    HeaderText = "Translation",
                    ReadOnly = true,
                    AutoSizeMode = gridViewTags.Columns.Contains("Image") ? DataGridViewAutoSizeColumnMode.AllCellsExceptHeader : DataGridViewAutoSizeColumnMode.Fill
                });
                await FillTranslation(gridViewAllTags);
                await FillTranslation(gridViewTags);
            }
            else
            {
                gridViewAllTags.Columns.Remove("Translation");
                gridViewTags.Columns.Remove("Translation");
            }
        }

        //private int findIndex = -1;
        private void toolStripButton13_Click(object sender, EventArgs e)
        {
            SetFilter();
        }

        private HashSet<string> GetSelectedTags()
        {
            HashSet<string> findTags = new HashSet<string>();
            for (int i = 0; i < gridViewAllTags.SelectedCells.Count; i++)
            {
                int row = gridViewAllTags.SelectedCells[i].RowIndex;
                string value = (string)gridViewAllTags.Rows[row].Cells[0].Value;
                if (!findTags.Contains(value))
                    findTags.Add(value);
            }
            return findTags;
        }

        private void SetFilter()
        {
            isLoading = true;
            if (gridViewAllTags.SelectedCells.Count > 0)
            {
                if (isFiltered)
                {
                    ResetFilter();
                }
                gridViewDS.DataSource = Program.DataManager.GetDataSource(DatasetManager.OrderType.Name, filterAnd, GetSelectedTags());
                if (gridViewDS.RowCount == 0)
                    gridViewTags.Rows.Clear();
                isFiltered = true;
                toolStripButton14.Enabled = true;
            }
            isLoading = false;
        }

        private void ResetFilter()
        {
            isLoading = true;
            if (isFiltered)
            {
                gridViewDS.DataSource = Program.DataManager.GetDataSource();
                isFiltered = false;
                toolStripButton14.Enabled = false;
            }
            isLoading = false;
        }

        private void toolStripButton14_Click(object sender, EventArgs e)
        {
            ResetFilter();
        }

        private void dataGridView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                toolStripButton3.PerformClick();
            }
            else if (e.KeyCode == Keys.Insert)
            {
                toolStripButton2.PerformClick();
            }
        }

        private void loadLossFromFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() != DialogResult.OK)
                return;
            Program.DataManager.LoadLossFromFile(openFileDialog.FileName);
            gridViewDS.DataSource = Program.DataManager.GetDataSource();
        }

        private async void toolStripButton15_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                gridViewTags.Rows.Clear();
                string text = Clipboard.GetText();
                string[] lines = text.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                    gridViewTags.Rows.Add(lines[i].ToLower().Trim());

                if (isTranslate)
                    await FillTranslation(gridViewTags);
            }
        }

        private void toolStripButton16_Click(object sender, EventArgs e)
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < gridViewTags.RowCount; i++)
                lines.Add((string)gridViewTags[0, i].Value);
            Form_Edit fPrint = new Form_Edit();
            fPrint.textBox1.Text = string.Join(", ", lines.Distinct().Where(a => !String.IsNullOrWhiteSpace(a)));
            fPrint.Show();
        }

        private void dataGridView1_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (gridViewTags.CurrentCell.ColumnIndex == 0)
            {
                AutoCompleteTextBox autoText = e.Control as AutoCompleteTextBox;
                if (autoText != null)
                {
                    //autoText.SetParent(gridViewTags);
                    //autoText.Values = Program.TagsList.tagList;
                    //autoText.Location = new Point(10, 10);
                    //autoText.Size = new Size(25, 75);
                    //autoText.AutoCompleteMode = AutoCompleteMode.Suggest;
                    //autoText.AutoCompleteSource = AutoCompleteSource.CustomSource;
                    //autoText.AutoCompleteCustomSource = Program.TagsList.Tags;
                }
            }
        }

        private void toolStripButton17_Click(object sender, EventArgs e)
        {
            if (gridViewDS.SelectedRows.Count != 1)
            {
                MessageBox.Show("Select one image!");
                return;
            }
            List<string> tags = new List<string>();
            for (int i = 0; i < gridViewTags.RowCount; i++)
            {
                tags.Add((string)gridViewTags[0, i].Value);
            }
            if (MessageBox.Show("Set tag list to empty images only?\nYes - only empty, No - to all images.", "Tag setting option", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Program.DataManager.SetTagListToAll(tags, true);
            }
            else
            {
                Program.DataManager.SetTagListToAll(tags, false);
            }
            Program.DataManager.UpdateData();
            BindTagList();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Program.DataManager != null && Program.DataManager.IsDataSetChanged())
            {
                DialogResult result = MessageBox.Show("The dataset has been changed,\ndo you want to save the changes?", "Saving changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    Program.DataManager.SaveAll();
                }
                else if (result == DialogResult.Cancel)
                    e.Cancel = true;
            }
        }

        private void dataGridView2_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1 || e.ColumnIndex == -1)
                return;
            AddSelectedAllTagsToImageTags();
        }

        private void dataGridView3_DataSourceChanged(object sender, EventArgs e)
        {

        }

        private void ApplyDataSetGridStyle()
        {
            for (int i = 0; i < gridViewDS.ColumnCount; i++)
            {
                if (gridViewDS.Columns[i].ValueType == typeof(Image))
                {
                    ((DataGridViewImageColumn)gridViewDS.Columns[i]).ImageLayout = DataGridViewImageCellLayout.NotSet;
                    gridViewDS.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                }
                if (gridViewDS.Columns[i].Name == "Loss" || gridViewDS.Columns[i].Name == "LastLoss")
                {
                    gridViewDS.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader;
                    gridViewDS.Columns[i].Visible = Program.DataManager.IsLossLoaded;
                }

            }
        }

        private void dataGridView3_SelectionChanged(object sender, EventArgs e)
        {
            if (isLoading)
            {
                LoadSelectedImageToGrid();
            }
            else
            {
                if (lastGridViewTagsHash != -1)
                {
                    if (lastGridViewTagsHash != GetgridViewTagsHash())
                    {
                        if (MessageBox.Show("The list of tags has been changed. Save changes?", "Saving changes",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            ApplyTagsChanges();
                        }
                    }
                }
                LoadSelectedImageToGrid();
            }
            lastGridViewTagsHash = GetgridViewTagsHash();
        }


        private int GetgridViewTagsHash()
        {
            List<string> tags = new List<string>();
            for (int i = 0; i < gridViewTags.RowCount; i++)
            {
                tags.Add((string)gridViewTags["ImageTags", i].Value);
            }
            return string.Join("|", tags).GetHashCode();
        }

        private void dataGridViewTags_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != -1 && e.RowIndex != -1)
            {
                if (gridViewTags.Columns.Contains("Image"))
                {
                    if (e.RowIndex != previewRowIndex)
                    {
                        var dataItem = Program.DataManager.DataSet[(string)gridViewTags["Image", e.RowIndex].Value];
                        previewPicBox.Size = new Size(Program.Settings.PreviewSize, Program.Settings.PreviewSize);
                        previewPicBox.Image = dataItem.Img;
                        previewPicBox.SizeMode = PictureBoxSizeMode.AutoSize;
                        previewPicBox.Location = new Point(splitContainer1.Panel2.Location.X, PointToClient(Cursor.Position).Y);

                        if (!this.Controls.ContainsKey("previewPicBox"))
                        {
                            this.Controls.Add(previewPicBox);
                        }
                        previewPicBox.BringToFront();
                        previewRowIndex = e.RowIndex;
                    }
                }
                else
                {
                    if (this.Controls.ContainsKey("previewPicBox"))
                    {
                        this.Controls.RemoveByKey("previewPicBox");
                        previewRowIndex = -1;
                    }
                }
            }
        }

        private void dataGridViewTags_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            if (this.Controls.ContainsKey("previewPicBox"))
            {
                this.Controls.RemoveByKey("previewPicBox");
                previewRowIndex = -1;
            }
        }

        private void toolStripButton18_Click(object sender, EventArgs e)
        {
            if (filterAnd)
            {
                filterAnd = false;
                toolStripButton18.Image = Properties.Resources.ORIcon;
            }
            else
            {
                filterAnd = true;
                toolStripButton18.Image = Properties.Resources.ANDIcon;
            }
        }

        private void gridViewTags_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (gridViewTags.Columns["ImageTags"].Index == e.ColumnIndex && e.RowIndex != -1)
            {
                string editedValue = (string)gridViewTags[e.ColumnIndex, e.RowIndex].Value;
                if (gridViewDS.SelectedRows.Count == 1)
                {
                    for (int i = 0; i < gridViewTags.RowCount; i++)
                    {
                        if (i != e.RowIndex && (string)gridViewTags[e.ColumnIndex, i].Value == editedValue)
                        {
                            gridViewTags.Rows.RemoveAt(e.RowIndex);
                        }
                    }
                }
                else if (gridViewDS.SelectedRows.Count > 1)
                {
                    if (string.IsNullOrEmpty((string)gridViewTags["Image", e.RowIndex].Value))
                    {
                        MessageBox.Show("Image name must be filled!");
                        gridViewTags.Rows.RemoveAt(e.RowIndex);
                    }
                    else
                    {
                        gridViewTags["Image", e.RowIndex].Tag = gridViewTags["ImageTags", e.RowIndex].Value;
                    }
                }
            }
        }

        private void toolStripButton19_Click(object sender, EventArgs e)
        {
            AddSelectedAllTagsToImageTags();
        }

        private void AddTagSingleSelectedMode(string tag)
        {
            if (gridViewDS.SelectedRows.Count != 1)
            {
                statusLabel.Text = "The number of selected images is not equal to 1";
                return;
            }

            for (int i = 0; i < gridViewTags.RowCount; i++)
            {
                if ((string)gridViewTags["ImageTags", i].Value == tag)
                {
                    return;
                }
            }
            gridViewTags.Rows.Add(tag);
        }

        private void AddTagMultiselectedMode(string tag)
        {
            if (gridViewDS.SelectedRows.Count < 2)
            {
                statusLabel.Text = "The number of selected images must be greater than 1";
                return;
            }
            List<string> selectedImages = new List<string>();
            for (int i = 0; i < gridViewDS.SelectedRows.Count; i++)
            {
                selectedImages.Add((string)gridViewDS.SelectedRows[i].Cells["Name"].Value);
            }

            selectedImages.Sort(new FileNamesComparer());

            List<KeyValuePair<int, string>> alreadyContainsImages = new List<KeyValuePair<int, string>>();


            for (int i = 0; i < gridViewTags.RowCount; i++)
            {
                if ((string)gridViewTags.Rows[i].Tag == tag)
                {
                    alreadyContainsImages.Add(new KeyValuePair<int, string>(i, (string)gridViewTags["Image", i].Value));
                }
            }
            if (alreadyContainsImages.Count > 0)
            {
                foreach (var item in alreadyContainsImages)
                {
                    selectedImages.Remove(item.Value);
                }
                int insertIndex = alreadyContainsImages.Max(a => a.Key) + 1;
                for (int i = 0; i < selectedImages.Count; i++)
                {
                    gridViewTags.Rows.Insert(insertIndex, "", selectedImages[i]);
                    gridViewTags.Rows[insertIndex].Tag = tag;
                    gridViewTags["ImageTags", insertIndex].Tag = selectedImages[i];
                    gridViewTags["Image", insertIndex].Tag = tag;
                    insertIndex++;
                }
            }
            else
            {
                for (int i = 0; i < selectedImages.Count; i++)
                {
                    int rowIndex = gridViewTags.Rows.Add();
                    DataGridViewRow row = gridViewTags.Rows[rowIndex];
                    row.Tag = tag;
                    row.Cells["ImageTags"].Value = i == 0 ? tag : "";
                    row.Cells["ImageTags"].Tag = selectedImages[i];
                    row.Cells["Image"].Value = selectedImages[i];
                    row.Cells["Image"].Tag = tag;
                }
            }
        }

        private void RemoveTagFromImageTags(string tag)
        {
            if (gridViewDS.SelectedRows.Count == 0)
            {
                statusLabel.Text = "The number of selected images must be greater than 0";
                return;
            }

            for (int i = gridViewTags.RowCount - 1; i >= 0; i--)
            {
                if (gridViewDS.SelectedRows.Count == 1)
                {
                    if ((string)gridViewTags["ImageTags", i].Value == tag)
                    {
                        gridViewTags.Rows.RemoveAt(i);
                    }
                }
                else
                {
                    if ((string)gridViewTags.Rows[i].Tag == tag)
                    {
                        gridViewTags.Rows.RemoveAt(i);
                    }
                }


            }
        }

        private List<string> GetSelectedTagsInAllTags()
        {
            List<string> selectedTags = new List<string>();
            for (int i = 0; i < gridViewAllTags.SelectedCells.Count; i++)
            {
                var row = gridViewAllTags.SelectedCells[i].RowIndex;
                var tag = (string)gridViewAllTags.Rows[row].Cells[0].Value;
                if(!selectedTags.Contains(tag))
                    selectedTags.Add(tag);
            }
            return selectedTags;
        }

        private async void AddSelectedAllTagsToImageTags()
        {
            if (gridViewAllTags.SelectedCells.Count == 0 || gridViewDS.SelectedRows.Count == 0)
            {
                statusLabel.Text = "Images or tags not selected!";
                return;
            }
            foreach (var item in GetSelectedTagsInAllTags())
            {
                if (gridViewDS.SelectedRows.Count == 1)
                    AddTagSingleSelectedMode(item);
                else
                    AddTagMultiselectedMode(item);
            }
            if (isTranslate)
                await FillTranslation(gridViewTags);
        }

        private void RemoveSelectedAllTagsToImageTags()
        {
            if (gridViewAllTags.SelectedCells.Count == 0 || gridViewDS.SelectedRows.Count == 0)
            {
                statusLabel.Text = "Images or tags not selected!";
                return;
            }
            foreach (var item in GetSelectedTagsInAllTags())
            {
                RemoveTagFromImageTags(item);
            }
        }

        private void toolStripButton20_Click(object sender, EventArgs e)
        {
            RemoveSelectedAllTagsToImageTags();
        }

        private void toolStripButton21_Click(object sender, EventArgs e)
        {
            AddTagToAll(true);
        }

        private void toolStripButton22_Click(object sender, EventArgs e)
        {
            RemoveTagFromAll(true);
        }

        private void gridViewDS_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.ColumnIndex != -1)
            {
                if (Enum.IsDefined(typeof(DatasetManager.OrderType), gridViewDS.Columns[e.ColumnIndex].Name))
                {
                    isLoading = true;
                    gridViewDS.DataSource = Program.DataManager.GetDataSourceWithLastFilter((DatasetManager.OrderType)Enum.Parse(typeof(DatasetManager.OrderType), gridViewDS.Columns[e.ColumnIndex].Name));
                    isLoading = false;
                }
            }
        }

        private void toolStripButton23_Click(object sender, EventArgs e)
        {
            string searchedTag;
            if (gridViewDS.SelectedRows.Count == 1)
            {
                searchedTag = (string)gridViewTags["ImageTags", gridViewTags.CurrentCell.RowIndex].Value;
            }
            else if (gridViewDS.SelectedRows.Count > 1)
            {
                searchedTag = (string)gridViewTags.Rows[gridViewTags.CurrentCell.RowIndex].Tag;
            }
            else
                return;
            for (int i = 0; i < gridViewAllTags.RowCount; i++)
            {
                if (((string)gridViewAllTags[0, i].Value) == searchedTag)
                {
                    gridViewAllTags.ClearSelection();
                    gridViewAllTags.Rows[i].Selected = true;
                    if (i < gridViewAllTags.FirstDisplayedScrollingRowIndex || i > gridViewAllTags.FirstDisplayedScrollingRowIndex + gridViewAllTags.DisplayedRowCount(false))
                    {
                        gridViewAllTags.FirstDisplayedScrollingRowIndex = i;
                    }
                }
            }
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form_settings settings = new Form_settings();
            if (settings.ShowDialog() == DialogResult.OK)
            {
                statusLabel.Text = "Settings have been saved";
            }
            settings.Close();
        }

        private void gridViewTags_KeyPress(object sender, KeyPressEventArgs e)
        {

        }

        private void gridViewDS_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteImage();
            }
        }

        private void DeleteImage()
        {
            if (gridViewDS.SelectedRows.Count < 1)
                return;
            if (MessageBox.Show("Are you sure you want to delete the selected image file?", "Delete File",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
            {
                gridViewTags.Rows.Clear();
                ApplyTagsChanges();

                var scroll = gridViewDS.FirstDisplayedScrollingRowIndex;
                var select = gridViewDS.SelectedRows[0].Index;
                var selects = new List<DataItem>(); 
                var list = gridViewDS.DataSource as List<DataItem>;
                foreach (DataGridViewRow item in gridViewDS.SelectedRows)
                {
                    selects.Add(list[item.Index]);
                    var file = (string)item.Cells["ImageFilePath"].Value;
                    var tagFile = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".txt");
                    try
                    {
                        File.Delete(file);
                        File.Delete(tagFile);
                        Program.DataManager.Remove(file);
                    }
                    catch (Exception ex)
                    {

                    }
                }
                Program.DataManager.UpdateData();
                BindTagList();
                //gridViewDS.DataSource = Program.DataManager.GetDataSource();
                foreach (var item in selects)
                {
                    list.Remove(item);
                }
                gridViewDS.DataSource = null;
                gridViewDS.DataSource = list;
                if (gridViewDS.RowCount > 0)
                {
                    gridViewDS.FirstDisplayedScrollingRowIndex = scroll;
                    if (select >= gridViewDS.RowCount)
                    {
                        select = gridViewDS.RowCount - 1;
                    }
                    gridViewDS.ClearSelection();
                    gridViewDS.Rows[select].Selected = true;
                }
            }
        }

        private void gridViewDS_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if(e.Button == MouseButtons.Left && gridViewDS.SelectedRows.Count > 0)
            {
                var file = (string)gridViewDS.SelectedRows[0].Cells["ImageFilePath"].Value;
                ShowPreview(file);
            }
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (gridViewDS.SelectedRows.Count > 0)
            {
                var file = (string)gridViewDS.SelectedRows[0].Cells["ImageFilePath"].Value;
                ExplorerFile(file);
            }
        }

        private void gridViewDS_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex != -1 && e.RowIndex < gridViewDS.Rows.Count && e.Button == MouseButtons.Right)
            {
                gridViewDS.ClearSelection();
                gridViewDS.Rows[e.RowIndex].Selected = true;
                contextMenuStrip1.Show(MousePosition);
            }
        }

        [DllImport("shell32.dll", ExactSpelling = true)]
        private static extern void ILFree(IntPtr pidlList);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern IntPtr ILCreateFromPathW(string pszPath);

        [DllImport("shell32.dll", ExactSpelling = true)]
        private static extern int SHOpenFolderAndSelectItems(IntPtr pidlList, uint cild, IntPtr children, uint dwFlags);

        public static void ExplorerFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                IntPtr pidlList = ILCreateFromPathW(filePath);
                if (pidlList != IntPtr.Zero)
                {
                    try
                    {
                        SHOpenFolderAndSelectItems(pidlList, 0, IntPtr.Zero, 0);
                    }
                    catch { }
                    finally
                    {
                        ILFree(pidlList);
                    }
                }
                return;
            }

            if(Directory.Exists(filePath))
            {
                Process.Start(@"explorer.exe", "/select,\"" + filePath + "\"");
                return;
            }
            var dir = Path.GetDirectoryName(filePath);
            if (Directory.Exists(dir))
            {
                Process.Start(@"explorer.exe", "\"" + dir + "\"");
            }
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            DeleteImage();
        }

        private void gridView_Enter(object sender, EventArgs e)
        {
            if (sender is DataGridView grid)
                grid.BorderStyle = BorderStyle.FixedSingle;
        }

        private void gridView_Leave(object sender, EventArgs e)
        {
            if (sender is DataGridView grid)
                grid.BorderStyle = BorderStyle.Fixed3D;
        }
    }
}
