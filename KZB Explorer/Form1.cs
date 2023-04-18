using Be.Windows.Forms;
using KZB;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace KZB_Explorer
{
    public partial class Form1 : Form
    {
        private KZBFile kzb = null;
        private KZBFile.Entry selectedEntry = null;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        #region Menu
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new())
            {
                dialog.Filter = "Kanzi Studio Binary|*.kzb";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    LoadFile(dialog.FileName);
                }
            }
        }

        private void extractAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (kzb == null)
            {
                MessageBox.Show("Open a kzb file first.");
                return;
            }    

            using (FolderBrowserDialog dialog = new())
            {
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    foreach (KZBFile.Entry entry in kzb.Entries)
                    {
                        string folder = Path.Combine(dialog.SelectedPath, entry.Path.Replace('/', '\\'));
                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);
                        try
                        {
                            File.WriteAllBytes(Path.Combine(folder, $"{CorrectFileName(entry.Name)}.dat"), kzb.GetDataBytes(entry));
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("Failed to extract all files.");
                            break;
                        }
                    }
                    MessageBox.Show("Extracted all files.");
                }
            }
        }

        private void gitHubToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://github.com/theawesomecoder61/KZB-Explorer")
            {
                UseShellExecute = true
            });
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(Properties.Resources.Credits, $"KZB Explorer {Application.ProductVersion} by pineapples721");
        }
        #endregion

        private void fileTV_AfterSelect(object sender, TreeViewEventArgs e)
        {
            KZBFile.Entry entry = e.Node.Tag as KZBFile.Entry;
            saveAsBtn.Visible = entry != null;
            if (entry != null)
            {
                selectedEntry = entry;
                infoRTB.Text = string.Format("Name: {0}\nOffset: {1}\nSize: {2}", entry.Name, entry.Offset, entry.Size);
                hexBox.ByteProvider = new DynamicByteProvider(kzb.GetDataBytes(entry));
                saveObjBtn.Visible = kzb.GetEntryType(selectedEntry) == "Mesh Data";
            }
            else
            {
                selectedEntry = null;
                infoRTB.Clear();
                hexBox.ByteProvider = null;
            }
        }

        private void saveAsBtn_Click(object sender, EventArgs e)
        {
            if (kzb == null)
            {
                MessageBox.Show("Open a kzb file first.");
                return;
            }
            if (selectedEntry != null)
            {
                using (SaveFileDialog dialog = new())
                {
                    dialog.FileName = selectedEntry.Name;
                    dialog.Filter = "Data|*.dat";
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllBytes(dialog.FileName, kzb.GetDataBytes(selectedEntry));
                        MessageBox.Show("Saved raw data.");
                    }
                }
            }
        }

        private void saveObjBtn_Click(object sender, EventArgs e)
        {
            if (selectedEntry != null)
            {
                using (SaveFileDialog dialog = new())
                {
                    dialog.FileName = $"{selectedEntry.Name}.obj";
                    dialog.Filter = "Wavefront OBJ|*.obj";
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            kzb.SaveMeshAsOBJ(selectedEntry, dialog.FileName);
                            MessageBox.Show("Saved mesh as obj.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                }
            }
        }

        private void LoadFile(string file)
        {
            fileTV.Nodes.Clear();
            kzb = new(file);
            kzb.Read();
            BuildTree(kzb, new() { Text = kzb.Name });

            entriesSL.Text = $"{kzb.Entries.Count} entries";
            pathSL.Text = file;
        }

        private void BuildTree(KZBFile file, TreeNode rootNode)
        {
            foreach (KZBFile.Entry entry in file.Entries.OrderBy(x => x.FullPath))
                BuildTreeRecursive(file.Entries, entry.GetPathParts().Skip(1).ToList(), rootNode.Nodes, 1);
            fileTV.Nodes.Add(rootNode);

            UpdateTreeRecursively(file.Entries.OrderBy(x => x.FullPath), rootNode.Nodes);

            rootNode.Expand();
        }

        private void BuildTreeRecursive(List<KZBFile.Entry> entries, List<string> path, TreeNodeCollection nodes, int depth)
        {
            if (path.Count == 0)
                return;

            string name = path.ElementAt(0);
            path.RemoveAt(0);
            TreeNode[] matchingNodes = nodes.Find(name, true);
            if (matchingNodes == null || matchingNodes.Length == 0)
            {
                TreeNode node = nodes.Add(name, name);
                if (path.Count > 0)
                    BuildTreeRecursive(entries, path, node.Nodes, depth + 1);
            }
            else
            {
                BuildTreeRecursive(entries, path, matchingNodes[0].Nodes, depth + 1);
            }
        }

        private void UpdateTreeRecursively(IEnumerable<KZBFile.Entry> entries, TreeNodeCollection nodes)
        {
            if (nodes.Count == 0)
                return;

            foreach (TreeNode node in nodes)
            {
                node.Tag = entries.FirstOrDefault(x => x.FullPath == node.FullPath.Replace('\\', '/'));
                if (node.Tag == null && node.Nodes.Count > 0)
                {
                    node.BackColor = Color.LightGreen;
                }
                UpdateTreeRecursively(entries, node.Nodes);
            }
        }

        private string CorrectFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
