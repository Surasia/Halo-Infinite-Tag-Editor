﻿using Halo_Infinite_Tag_Editor.InfiniteRuntimeTagViewer.Controls;
using HavokScriptToolsCommon;
using InfiniteModuleEditor;
using InfiniteRuntimeTagViewer;
using InfiniteRuntimeTagViewer.Halo;
using InfiniteRuntimeTagViewer.Halo.TagObjects;
using InfiniteRuntimeTagViewer.Interface.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using ZTools;
using static InfiniteRuntimeTagViewer.Halo.TagObjects.TagLayouts;
using static ZTools.ZCommon;
using TagBlock = InfiniteRuntimeTagViewer.Interface.Controls.TagBlock;

namespace Halo_Infinite_Tag_Editor
{
    public partial class MainWindow : Window
    {
        // -HALO INFINITE TAG EDITOR-
        // 
        // I spent a great deal of time learning how Krevil's Infinite Module Editor (IME) works,
        // as well as working on Gamergottens IRTV. So, I decided to attempt to combine the two. IRTVs,
        // structures and tag reading is pretty much perfected, while IME can open and view all tags within
        // modules. So, I decied to combine the two to make this. Credits for the two repositories are below.
        // I will try to credit everything I didn't personally write as to not upset those who spent their 
        // time building some pretty awesome programs.
        //
        // Also, credit to soupstream and their Havok Script Disassembler. I decided to add it in here so we
        // no long have to extract tags with havok scripts to read them. Credit will be given on the tab as well.
        //
        // Krevil's Infinite Module Editor: https://github.com/Krevil/InfiniteModuleEditor
        // Gamergotten's Infinite Runtime Tag Viewer: https://github.com/Gamergotten/Infinite-runtime-tagviewer
        // Krevil's fork of Crauzer's OodleSharp: https://github.com/Krevil/OodleSharp
        // Soupstream's Havok-Script-Tools: https://github.com/soupstream/havok-script-tools
        //
        // Everything used from other projects will be placed in their own seperate folders, unless it's impossible
        // or more difficult to do so.

        // Used to reference in other classes.

        #region Initialization
        public static MainWindow? instance;
        private string deployPath = "";
        private Folder baseFolder = new Folder();
        public List<string> modulePaths = new List<string>();

        public class Folder
        {
            public string fullPath = "";
            public string folderName = "";
            public List<Folder> subfolders = new List<Folder>();
            public List<string> files = new List<string>();
            public static List<string> filePaths = new List<string>();
            public static List<string> folderPaths = new List<string>();

            public void Search()
            {
                try
                {
                    foreach (string file in Directory.GetFiles(fullPath))
                    {
                        if (file.EndsWith("module") && !filePaths.Contains(file))
                        {
                            instance.modulePaths.Add(file);
                            files.Add(file.Split('\\').Last());
                            filePaths.Add(file);
                        }
                    }

                    foreach (string folder in Directory.GetDirectories(fullPath))
                    {
                        folderPaths.Add(folder);
                        Folder subfolder = new Folder();
                        subfolder.fullPath = folder;
                        subfolder.folderName = folder.Split('\\').Last();
                        subfolder.Search();
                        subfolders.Add(subfolder);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            public int GetCount()
            {
                return filePaths.Count;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            // Set instance to this window.
            instance = this;

            // Don't know how settings work yet, this will be updated in the future.
            deployPath = AppSettings.Default.DeployPath;

            // Tries to find module files in the given path.
            if (FindModuleFiles())
            {
                CreateModuleTree(null, baseFolder);
            }
            else
            {
                StatusOut("No module files were found in given path...");
            }

            // Read and store info from the text files in the Files directory.
            InhaleTagNames();
        }

        public void InhaleTagNames()
        {
            string filename = Directory.GetCurrentDirectory() + @"\files\tagnames.txt";
            IEnumerable<string>? lines = System.IO.File.ReadLines(filename);
            foreach (string? line in lines)
            {
                string[] hexString = line.Split(" : ");
                if (!inhaledTags.ContainsKey(hexString[0]))
                {
                    TagInfo ti = new();
                    ti.TagID = hexString[0];
                    ti.AssetID = hexString[1];
                    ti.ModulePath = hexString[2];
                    ti.TagPath = hexString[3];
                    ti.TagGroup = hexString[4];
                    inhaledTags.Add(hexString[0], ti);
                }
            }
            Debug.WriteLine("");
        }

        private bool FindModuleFiles()
        {
            if (Directory.Exists(deployPath))
            {
                DeployPathBox.Text = deployPath;
                baseFolder.fullPath = deployPath;
                baseFolder.folderName = deployPath.Split('\\').Last();
                baseFolder.Search();

                if (baseFolder.GetCount() > 0)
                    return true;
                else
                    return false;
            }
            else
            {
                StatusOut("Unable to locate module files...");
                return false;
            }
        }

        private void CreateModuleTree(TreeViewItem? item, Folder currentFolder)
        {
            foreach (Folder folder in currentFolder.subfolders)
            {
                TreeViewItem tvFolder = new TreeViewItem();
                tvFolder.Header = folder.folderName;

                if (item == null)
                {
                    if (folder.files.Count > 0)
                    {
                        foreach (string file in folder.files)
                        {
                            TreeViewItem tvFile = new TreeViewItem();
                            tvFile.Header = file;
                            tvFile.Tag = folder.fullPath + "\\" + file;
                            tvFile.Selected += ModuleSelected;
                            ModuleTree.Items.Add(tvFile);
                        }
                    }
                    ModuleTree.Items.Add(tvFolder);
                }
                else
                {
                    if (folder.files.Count > 0)
                    {
                        foreach (string file in folder.files)
                        {
                            TreeViewItem tvFile = new TreeViewItem();

                            tvFile.Header = file;
                            tvFile.Tag = folder.fullPath + "\\" + file;
                            tvFile.Selected += ModuleSelected;
                            tvFolder.Items.Add(tvFile);
                        }
                    }
                    item.Items.Add(tvFolder);
                }

                if (folder.subfolders.Count > 0)
                    CreateModuleTree(tvFolder, folder);
            }
        }
        #endregion

        #region Window Controls
        // Anything that controls how the UI that doesn't fit in another group.
        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CommandBinding_Executed_Minimize(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
        }

        private void CommandBinding_Executed_Maximize(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.MaximizeWindow(this);
            RestoreButton.Visibility = Visibility.Visible;
            MaximizeButton.Visibility = Visibility.Collapsed;
        }

        private void CommandBinding_Executed_Restore(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.RestoreWindow(this);
            RestoreButton.Visibility = Visibility.Collapsed;
            MaximizeButton.Visibility = Visibility.Visible;
        }

        private void CommandBinding_Executed_Close(object sender, ExecutedRoutedEventArgs e)
        {
            SystemCommands.CloseWindow(this);
        }

        private void Move_Window(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void StatusOut(string message)
        {
            // Invoking dispatcher allows this to be called from other threads.
            Dispatcher.Invoke(new Action(() =>
            {
                StatusBlock.Text = message;
            }), DispatcherPriority.Background);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void SetPathClick(object sender, RoutedEventArgs e)
        {
            deployPath = DeployPathBox.Text;

            baseFolder = new Folder();
            ModuleTree.Items.Clear();

            if (FindModuleFiles())
            {
                AppSettings.Default.DeployPath = deployPath;
                CreateModuleTree(null, baseFolder);
            }
            else
            {
                ModuleTree.Items.Clear();
                StatusOut("No module files were found in given path...");
            }

            GC.Collect();
        }
        #endregion

        #region Tag List
        public class TagFolder
        {
            public string folderName = "";
            public SortedList<string, TagData>? tags = new SortedList<string, TagData>();
            public TreeViewItem? folder;
        }

        public class TagData
        {
            public string Header = "";
            public string Tag = "";
            public string Group = "";
        }

        private SortedList<string, TagFolder> tagFolders = new SortedList<string, TagFolder>();
        private FileStream? moduleStream;
        private Module? module;
        private string modulePath = "";
        private bool moduleOpen = false;

        private void ModuleSelected(object sender, RoutedEventArgs e)
        {
            ResetTagTree();
            TreeViewItem tv = (TreeViewItem)sender;
            LoadModule((string)tv.Tag);
        }

        private void OpenModuleClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Module File (*.module)|*.module";

            if (ofd.ShowDialog() == true)
            {
                ResetTagTree();

                LoadModule(ofd.FileName);
            }
        }

        private void CloseModuleClick(object sender, RoutedEventArgs e)
        {
            if (moduleOpen)
            {
                ResetTagTree();
                StatusOut("Module closed...");
            }
            else
                StatusOut("No module is loaded...");

        }

        private void BackupModuleClick(object sender, RoutedEventArgs e)
        {
            if (moduleOpen)
            {
                try
                {
                    SaveFileDialog sfd = new SaveFileDialog();
                    sfd.Filter = "Module File (*.module)|*.module";
                    sfd.FileName = "BACKUP_" + modulePath.Split("\\").Last();

                    if (sfd.ShowDialog() == true)
                    {
                        File.Copy(modulePath, sfd.FileName, true);

                    }
                    StatusOut("Backup successful: " + sfd.FileName);
                }
                catch
                {
                    StatusOut("Backup failed!");
                }
            }
            else
                StatusOut("A module must be opened to backup...");

        }

        private void SearchTagClick(object sender, RoutedEventArgs e)
        {
            string searchTerm = SearchBox.Text;
            int foundCount = 0; // Total matches found

            foreach (TagFolder folder in tagFolders.Values) // Iterate through folders
            {
                TreeViewItem? tvFolder = folder.folder; // Set variable for the folder
                if (tvFolder != null) // If its not null, continue
                {
                    if (folder.folderName.Contains(searchTerm)) // If the foldername contains the search term, make everything in that folder visible.
                    {
                        tvFolder.Visibility = Visibility.Visible; // Set visible

                        foreach (TreeViewItem tvTag in tvFolder.Items) // Set all children visible
                        {
                            tvTag.Visibility = Visibility.Visible;
                            foundCount++;
                        }
                    }
                    else
                    {
                        bool foundInFolder = false; // Check to see if the folder needs to be set invisible

                        foreach (TreeViewItem tvTag in tvFolder.Items) // Go through every child in the folder
                        {
                            if (((string)tvTag.Header).Contains(searchTerm)) // If found
                            {
                                tvTag.Visibility = Visibility.Visible; // Set visible
                                foundInFolder = true; // Set found in folder
                                foundCount++; // Increase count
                            }
                            else
                            {
                                tvTag.Visibility = Visibility.Collapsed; // Collapse Visibility
                            }

                            if (!foundInFolder)
                                tvFolder.Visibility = Visibility.Collapsed;
                            else
                                tvFolder.Visibility = Visibility.Visible;
                        }
                    }
                }
            }
            StatusOut("Found " + foundCount + " matches for: \"" + SearchBox.Text + "\"");
        }

        private void ResetTagTree()
        {
            ResetTagViewer();

            if (moduleOpen || module != null)
            {
                fileStreamOpen = false;
                moduleOpen = false;

                if (moduleStream != null)
                    moduleStream.Close();

                moduleStream = null;
                module = null;

                foreach (TagFolder tf in tagFolders.Values)
                {
                    TagsTree.Items.Remove(tf.folder);
                    tf.folder = null;
                    tf.tags = null;
                }
                tagFolders.Clear();
                tagFolders = new SortedList<string, TagFolder>();
            }

            GC.Collect();
        }

        private async void LoadModule(string path)
        {
            try
            {
                StatusOut("Loading module...");
                List<string> folders = new();
                Task loadModule = new Task(() =>
                {
                    moduleStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                    module = ModuleEditor.ReadModule(moduleStream);

                    fileStreamOpen = true;
                    modulePath = path;

                    foreach (KeyValuePair<string, ModuleFile> tag in module.ModuleFiles)
                    {
                        string[] pathSplit = tag.Key.Split("\\");
                        string head = tag.Key;

                        if (pathSplit.Count() > 2)
                        {
                            List<string> head1 = tag.Key.Split("\\").TakeLast(2).ToList();
                            head = head1[0] + "\\" + head1[1];
                        }

                        string group = ReverseString(ASCIIEncoding.ASCII.GetString(BitConverter.GetBytes(tag.Value.FileEntry.ClassId)));

                        if (!groupNames.ContainsKey(group) && tag.Key.Contains("."))
                            groupNames.Add(group, tag.Key.Split(".").Last());

                        TagData newTag = new()
                        {
                            Header = head,
                            Tag = tag.Key,
                            Group = group
                        };

                        string folderName = group;
                        if (groupNames.ContainsKey(group))
                            folderName = $"{group} ({groupNames[group]})";
                        else
                            folderName = $"{group} (Unknown)";

                        if (!tagFolders.ContainsKey(group))
                        {
                            TagFolder newFolder = new();
                            newFolder.folderName = folderName;
                            newFolder.tags.Add(tag.Key, newTag);
                            tagFolders.Add(group, newFolder);
                        }
                        else
                        {
                            TagFolder tf = tagFolders[group];
                            tf.folderName = folderName;
                            if (!tf.tags.ContainsKey(head))
                                tf.tags.Add(tag.Key, newTag);
                        }
                    }
                });
                loadModule.Start();
                await loadModule;
                loadModule.Dispose();

                foreach (TagFolder tf in tagFolders.Values)
                {
                    TreeViewItem tvFolder = new();
                    tvFolder.Header = tf.folderName;
                    tf.folder = tvFolder;

                    foreach (TagData tag in tf.tags.Values)
                    {
                        TreeViewItem tvTag = new TreeViewItem();
                        tvTag.Header = tag.Header;
                        tvTag.Tag = tag;
                        tvTag.ToolTip = tag.Tag;
                        tvTag.Selected += TagSelected;
                        tvFolder.Items.Add(tvTag);
                    }
                    TagsTree.Items.Add(tvFolder);
                }

                moduleOpen = true;

                StatusOut("Opened module: " + path);
            }
            catch
            {
                ResetTagTree();
                StatusOut("Failed to open module!");
            }
        }
        #endregion

        #region Tag Viewer Events
        private bool tagOpen = false;
        private string TagGroup = "";
        private bool fileStreamOpen = false;
        private bool tagOpenedFromModule = false;
        private string tagFileName = "";
        private string curTagID = "";
        private MemoryStream? tagStream;
        private FileStream? tagFileStream;
        public static ModuleFile? moduleFile;

        private void TagSelected(object sender, RoutedEventArgs e)
        {
            TreeViewItem tv = (TreeViewItem)sender;

            if (tagOpen)
            {
                ResetTagViewer();
            }

            if (fileStreamOpen)
            {
                TagData tagData = (TagData)tv.Tag;
                tagFileName = tagData.Tag.Replace("\0", String.Empty);
                tagStream = ModuleEditor.GetTag(module, moduleStream, tagFileName);
                moduleFile = new ModuleFile();
                module.ModuleFiles.TryGetValue(module.ModuleFiles.Keys.ToList().Find(x => x.Contains(tagFileName)), out moduleFile);
                moduleFile.Tag = ModuleEditor.ReadTag(tagStream, tagFileName.Replace("\0", String.Empty), moduleFile);
                moduleFile.Tag.Name = tagFileName;
                moduleFile.Tag.ShortName = tagFileName.Split("\\").Last();
                curTagID = Convert.ToHexString(GetDataFromByteArray(4, 8, moduleFile.Tag.TagData));
                tagOpen = true;
                tagOpenedFromModule = true;

                BuildTagViewer(tagData);

                ReadScript(tagStream.ToArray());

                ModuleBlock.Text = modulePath.Split("\\").Last();
                TagNameBlock.Text = tagFileName.Split("\\").Last();
                TagIDBlock.Text = curTagID;
                DataOffsetBlock.Text = moduleFile.Tag.Header.HeaderSize.ToString();
            }
        }

        private void ReadScript(byte[] data)
        {
            try
            {
                string search = "1B4C7561510E";
                int index = 0;
                bool found = false;
                for (int i = 0; i < data.Length - 7; i++)
                {
                    string testBytes = Convert.ToHexString(data[i..(i + 6)]);

                    if (testBytes == search)
                    {
                        index = i;
                        found = true;
                        break;
                    }
                }

                // Doing it this way so any tag with a lua script is read.
                if (found)
                {
                    byte[] lua = data[index..data.Length];
                    var disassembler = new HksDisassembler(lua);
                    luaView.Text = disassembler.Disassemble();
                }
            }
            catch (Exception ex)
            {
                StatusOut("Error reading lua script: " + ex.Message);
            }
        }

        private void SaveScript(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new();
            sfd.Filter = "Lua Script (*.lua)|*.lua";
            sfd.FileName = TagNameBlock.Text.Split(".").First() + ".lua";
            if (sfd.ShowDialog() == true)
            {
                File.WriteAllText(sfd.FileName, luaView.Text);
            }
        }

        private void TagViewerSearchClick(object sender, RoutedEventArgs e)
        {
            string searchTerm = TagViewerSearchBox.Text;
            int foundCount = 0;

            foreach (KeyValuePair<string, Control> control in tagViewerControls)
            {
                if (searchTerm == "" || searchTerm == " ")
                {
                    control.Value.Visibility = Visibility.Visible;
                }
                else
                {
                    TagValueData tvd = tagValueData[control.Key];
                    if (tvd.Name.ToLower().Contains(searchTerm))
                        control.Value.Visibility = Visibility.Visible;
                    else
                        control.Value.Visibility = Visibility.Collapsed;
                }

            }
            StatusOut("Found " + foundCount + " matches for: \"" + SearchBox.Text + "\"");
        }

        private void OpenTagClick(object sender, RoutedEventArgs e)
        {
            if (tagOpen)
            {
                ResetTagViewer();
            }

            if (moduleOpen)
            {
                ResetTagTree();
            }

            OpenFileDialog ofd = new OpenFileDialog();

            if (ofd.ShowDialog() == true)
            {
                ReadScript(File.ReadAllBytes(ofd.FileName));

                tagFileStream = new FileStream(ofd.FileName, FileMode.Open);
                moduleFile = new ModuleFile();
                moduleFile.Tag = ModuleEditor.ReadTag(tagFileStream, ofd.SafeFileName);
                moduleFile.Tag.Name = ofd.FileName;
                tagFileName = ofd.FileName;
                moduleFile.Tag.ShortName = tagFileName.Split("\\").Last();

                tagOpen = true;
                tagOpenedFromModule = false;
                curTagID = Convert.ToHexString(GetDataFromByteArray(4, 8, moduleFile.Tag.TagData));
                ModuleBlock.Text = modulePath.Split("\\").Last();
                TagNameBlock.Text = tagFileName.Split("\\").Last();
                TagGroup = tagFileName.Split(".").Last();
                TagIDBlock.Text = curTagID;
                DataOffsetBlock.Text = moduleFile.Tag.Header.HeaderSize.ToString();
                TagData newTag = new()
                {
                    Header = ofd.FileName,
                    Tag = ofd.FileName,
                    Group = TagGroup
                };

                BuildTagViewer(newTag);

                StatusOut("Tag opened from file: " + ofd.FileName);
            }
        }

        private void SaveTagClick(object sender, RoutedEventArgs e)
        {
            if (tagOpenedFromModule)
            {
                if (ModuleEditor.WriteTag(moduleFile, tagStream, moduleStream))
                {
                    StatusOut("Tag successfully saved to module!");
                }
                else
                {
                    StatusOut("Error saving tag to module!");
                }
            }
            else
            {
                StatusOut("Saving tag file not yet supported!");
            }
        }

        private void ImportTagClick(object sender, RoutedEventArgs e)
        {
            if (tagOpen)
            {
                OpenFileDialog ofd = new OpenFileDialog();

                if (ofd.ShowDialog() == true)
                {
                    FileStream fs = new FileStream(ofd.FileName, FileMode.Open);
                    byte[] data = new byte[fs.Length];
                    fs.Seek(0, SeekOrigin.Begin);
                    fs.Read(data, 0, (int)fs.Length);

                    tagStream.Seek(0, SeekOrigin.Begin);
                    tagStream.Write(data, 0, data.Length);

                    StatusOut("Tag imported: " + ofd.FileName);
                }
            }
            else
            {
                StatusOut("A tag needs to be open to overwrite...");
            }

        }

        private void ExportTagClick(object sender, RoutedEventArgs e)
        {
            if (tagOpen)
            {
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.FileName = tagFileName.Split("\\").Last();

                if (sfd.ShowDialog() == true)
                {
                    FileStream outputStream = new FileStream(sfd.FileName, FileMode.Create);
                    tagStream.Seek(0, SeekOrigin.Begin);
                    tagStream.CopyTo(outputStream);
                    outputStream.Close();
                    StatusOut("Tag exported: " + sfd.FileName);
                }
            }
            else
            {
                StatusOut("A tag needs to be open to export...");
            }
        }

        private void CloseTagClick(object sender, RoutedEventArgs e)
        {
            if (tagOpen)
            {
                ResetTagViewer();
                StatusOut("Tag closed...");
            }
            else
            {
                StatusOut("A tag isn't open...");
            }
        }

        private void ResetTagViewer()
        {
            if (tagOpen)
            {
                if (tagOpenedFromModule)
                {
                    tagStream.Close();
                }
                else
                {
                    tagFileStream.Close();
                }

                tagOpen = false;
                tagOpenedFromModule = false;
                ModuleBlock.Text = "";
                TagNameBlock.Text = "";
                TagIDBlock.Text = "";
                DataOffsetBlock.Text = "";
                TagViewer.Children.Clear();
                tagViewerControls.Clear();
                tagViewerControls = new();
                tagValueData.Clear();
                tagValueData = new();
                curDataBlockInd = 1;
                tagStream = null;
                tagFileStream = null;
                moduleFile = null;
                luaView.Clear();
            }
        }


        #endregion

        #region Tag Viewer Controls
        public class TagValueData
        {
            public string Name = "";
            public string? Type;
            public string? ControlType;
            public long Offset = 0;
            public string? OffsetChain;
            public byte[]? Value;
            public long? Size;
            public int DataBlockIndex = 0;
            public int ChildCount = 0;
        }

        private Dictionary<string, TagValueData> tagValueData = new();
        private Dictionary<string, Control> tagViewerControls = new();
        private int curDataBlockInd = 1;

        private async void BuildTagViewer(TagData tagData)
        {
            try
            {
                curDataBlockInd = 1;

                StatusOut("Retrieving tag data...");

                IRTV_TagStruct tagStruct = new()
                {
                    Datnum = "",
                    ObjectId = curTagID,
                    TagGroup = tagData.Group,
                    TagData = 0,
                    TagTypeDesc = "",
                    TagFullName = curTagID,
                    TagFile = moduleFile.Tag.ShortName,
                    unloaded = false
                };

                Dictionary<long, TagLayouts.C> tagDefinitions = TagLayouts.Tags(tagStruct.TagGroup);
                Task loadTag = new Task(() =>
                {
                    GetTagValueData(tagDefinitions, 0, 0, curTagID + ":");
                });

                loadTag.Start();
                await loadTag;
                loadTag.Dispose();

                StatusOut("Building tag viewer...");
                CreateTagControls(tagStruct, 0, tagDefinitions, tagStruct.TagData, TagViewer, curTagID + ":", null, false);
                StatusOut("Opened tag from module: " + tagFileName.Split("\\").Last());

                GC.Collect();
            }
            catch (Exception ex)
            {
                StatusOut("Error loading tag: " + ex.Message);
            }

        }

        private void GetTagValueData(Dictionary<long, TagLayouts.C> tagDefinitions, long address, long startingTagOffset, string offsetChain)
        {
            foreach (KeyValuePair<long, TagLayouts.C> entry in tagDefinitions)
            {
                entry.Value.MemoryAddress = address + entry.Key;
                entry.Value.AbsoluteTagOffset = offsetChain + "," + (entry.Key + startingTagOffset);
                try
                {
                    string name = "";
                    if (entry.Value.N != null)
                        name = entry.Value.N;
                    TagValueData curTagData = new()
                    {
                        Name = name,
                        ControlType = entry.Value.T,
                        Type = entry.Value.T,
                        OffsetChain = entry.Value.AbsoluteTagOffset,
                        Offset = entry.Value.MemoryAddress,
                        Value = GetDataFromFile((int)entry.Value.S, entry.Value.MemoryAddress),
                        Size = (int)entry.Value.S,
                        ChildCount = 0,
                        DataBlockIndex = 0
                    };

                    if (entry.Value.T == "Tagblock" || entry.Value.T == "FUNCTION")
                    {
                        int blockIndex = 0;
                        int childCount = BitConverter.ToInt32(GetDataFromFile(20, entry.Value.MemoryAddress), 16); ;

                        if (curTagData.Type == "Tagblock" && childCount < 100000)
                        {
                            if (childCount > 0 && entry.Value.N != "material constants")
                            {
                                blockIndex = curDataBlockInd;
                                curDataBlockInd++;
                                curTagData.ChildCount = childCount;
                                curTagData.DataBlockIndex = blockIndex;

                                for (int i = 0; i < childCount; i++)
                                {
                                    long newAddress = (long)moduleFile.Tag.DataBlockArray[curTagData.DataBlockIndex].Offset - (long)moduleFile.Tag.DataBlockArray[0].Offset + (entry.Value.S * i);
                                    GetTagValueData(entry.Value.B, newAddress, newAddress + entry.Value.S * i, curTagData.OffsetChain);
                                }
                            }
                        }
                        else if (curTagData.Type == "FUNCTION")
                        {
                            curTagData.ChildCount = childCount;
                            childCount = BitConverter.ToInt32(GetDataFromFile(4, entry.Value.MemoryAddress + 20));
                            if (childCount > 0)
                            {
                                blockIndex = curDataBlockInd;
                                curDataBlockInd++;

                                curTagData.ChildCount = childCount;
                                curTagData.DataBlockIndex = blockIndex;
                            }

                        }
                    }

                    tagValueData.Add(curTagData.OffsetChain, curTagData);
                }
                catch (Exception ex)
                {
                    StatusOut("Error loading getting tag data: " + ex.Message);
                    Debug.WriteLine(ex.Message);
                }
            }
        }

        private void CreateTagControls(IRTV_TagStruct tagStruct, long startingTagOffset, Dictionary<long, TagLayouts.C> tagDefinitions, long address, StackPanel parentpanel, string offsetChain, TagBlock? tb, bool isTagBlock)
        {
            KeyValuePair<long, TagLayouts.C> prevEntry = new();

            foreach (KeyValuePair<long, TagLayouts.C> entry in tagDefinitions)
            {
                entry.Value.MemoryAddress = address + entry.Key;
                entry.Value.AbsoluteTagOffset = offsetChain + "," + (entry.Key + startingTagOffset);

                TagValueData tvd = new();

                if (tagValueData.ContainsKey(entry.Value.AbsoluteTagOffset))
                {
                    tvd = tagValueData[entry.Value.AbsoluteTagOffset];

                    try
                    {
                        if (!tvd.Name.ToLower().Contains("generated_pad"))
                        {
                            if (tvd.ControlType == "Comment")
                            {
                                if (prevEntry.Value != null && prevEntry.Value.N != null && tvd.Name.ToLower().Trim() != "function")
                                {
                                    string prevName = prevEntry.Value.N;
                                    string prevType = prevEntry.Value.T;
                                    string curName = tvd.Name;

                                    prevName = (prevName.ToLower()).Replace(" ", "");
                                    curName = (curName.ToLower()).Replace(" ", "");

                                    CommentBlock? vb0 = new();
                                    vb0.Tag = "";
                                    vb0.comment.Text = tvd.Name;

                                    if (prevType == "Comment")
                                    {
                                        if (prevName != curName)
                                        {
                                            parentpanel.Children.Add(vb0);
                                            tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb0);
                                        }

                                    }
                                    else
                                    {
                                        parentpanel.Children.Add(vb0);
                                        tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb0);
                                    }
                                }
                                else
                                {
                                    CommentBlock? vb0 = new();
                                    vb0.comment.Text = tvd.Name;
                                    parentpanel.Children.Add(vb0);
                                    tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb0);
                                }
                            }
                            else if (tvd.ControlType == "Byte")
                            {
                                TagValueBlock? vb = new();
                                vb.value_name.Text = tvd.Name;
                                vb.value.Text = tvd.Value[0].ToString();
                                vb.value.Tag = entry.Value.AbsoluteTagOffset;
                                vb.value.TextChanged += VBTextChange;
                                vb.value.ToolTip = "Type: " + tvd.Type;
                                parentpanel.Children.Add(vb);

                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                            }
                            else if (tvd.ControlType == "2Byte")
                            {
                                TagValueBlock? vb = new();
                                vb.value_name.Text = tvd.Name;
                                vb.value.Text = BitConverter.ToInt16(tvd.Value).ToString();
                                vb.value.Tag = entry.Value.AbsoluteTagOffset;
                                vb.value.TextChanged += VBTextChange;
                                vb.value.ToolTip = "Type: " + tvd.Type;
                                parentpanel.Children.Add(vb);

                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                            }
                            else if (tvd.ControlType == "4Byte")
                            {
                                TagValueBlock? vb = new();
                                vb.value_name.Text = tvd.Name;
                                vb.value.Text = BitConverter.ToInt32(tvd.Value).ToString();
                                vb.value.Tag = entry.Value.AbsoluteTagOffset;
                                vb.value.TextChanged += VBTextChange;
                                vb.value.ToolTip = "Type: " + tvd.Type;
                                parentpanel.Children.Add(vb);

                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                            }
                            else if (tvd.ControlType == "Float")
                            {
                                TagValueBlock? vb = new();
                                vb.value_name.Text = tvd.Name;
                                vb.value.Text = BitConverter.ToSingle(tvd.Value).ToString();
                                vb.value.Tag = entry.Value.AbsoluteTagOffset;
                                vb.value.TextChanged += VBTextChange;
                                vb.value.ToolTip = "Type: " + tvd.Type;
                                parentpanel.Children.Add(vb);

                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                            }
                            else if (tvd.ControlType == "Pointer")
                            {
                                TagValueBlock? vb = new();
                                vb.value_name.Text = tvd.Name;
                                vb.value.Text = Convert.ToHexString(tvd.Value);
                                vb.value.Tag = entry.Value.AbsoluteTagOffset;
                                vb.value.ToolTip = "Type: " + tvd.Type;
                                vb.value.IsReadOnly = true;
                                parentpanel.Children.Add(vb);

                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                            }
                            else if (tvd.ControlType == "mmr3Hash")
                            {
                                TagValueBlock? vb = new();
                                vb.value_name.Text = tvd.Name;
                                vb.value.Text = Convert.ToHexString(tvd.Value);
                                vb.value.Tag = entry.Value.AbsoluteTagOffset;
                                vb.value.TextChanged += VBTextChange;
                                vb.value.ToolTip = "Type: " + tvd.Type;
                                parentpanel.Children.Add(vb);

                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                            }
                            else if (tvd.ControlType == "String")
                            {
                                TagValueBlock? vb = new();
                                vb.value_name.Text = tvd.Name;
                                vb.value.Text = Encoding.UTF8.GetString(tvd.Value);
                                vb.value.Tag = entry.Value.AbsoluteTagOffset;
                                vb.value.TextChanged += VBTextChange;
                                vb.value.ToolTip = "Type: " + tvd.Type;
                                parentpanel.Children.Add(vb);

                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                            }
                            else if (tvd.ControlType == "EnumGroup")
                            {
                                if (!(entry.Value is TagLayouts.EnumGroup))
                                    continue;

                                TagLayouts.EnumGroup? fg3 = entry.Value as TagLayouts.EnumGroup;
                                EnumBlock eb = new EnumBlock();
                                eb.enums.Tag = entry.Value.AbsoluteTagOffset;
                                eb.enums.SelectionChanged += EnumBlockChanged;

                                foreach (KeyValuePair<int, string> gvsdahb in fg3.STR)
                                {
                                    ComboBoxItem cbi = new() { Content = gvsdahb.Value };
                                    eb.enums.Items.Add(cbi);
                                }

                                if (fg3.A == 1)
                                {
                                    int test_this = (int)tvd.Value[0];
                                    if (eb.enums.Items.Count >= test_this)
                                    {
                                        eb.enums.SelectedIndex = test_this;
                                    }
                                }
                                else if (fg3.A == 2)
                                {
                                    int test_this = BitConverter.ToInt16(tvd.Value);
                                    if (eb.enums.Items.Count >= test_this)
                                    {
                                        eb.enums.SelectedIndex = test_this;
                                    }
                                }
                                else if (fg3.A == 4)
                                {
                                    int test_this = (int)BitConverter.ToInt32(tvd.Value);
                                    if (eb.enums.Items.Count >= test_this)
                                    {
                                        eb.enums.SelectedIndex = test_this;
                                    }
                                }
                                eb.value_name.Text = fg3.N;

                                parentpanel.Children.Add(eb);
                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, eb);
                            }
                            else if (tvd.ControlType == "Flags")
                            {
                                byte flags_value = (byte)tvd.Value[0];

                                TagsFlags? vb = new();
                                vb.Tag = entry.Value.AbsoluteTagOffset;
                                vb.flag1.IsChecked = flags_value.GetBit(0);
                                vb.flag2.IsChecked = flags_value.GetBit(1);
                                vb.flag3.IsChecked = flags_value.GetBit(2);
                                vb.flag4.IsChecked = flags_value.GetBit(3);
                                vb.flag5.IsChecked = flags_value.GetBit(4);
                                vb.flag6.IsChecked = flags_value.GetBit(5);
                                vb.flag7.IsChecked = flags_value.GetBit(6);
                                vb.flag8.IsChecked = flags_value.GetBit(7);
                                vb.flag1.Click += FlagGroupChange;
                                vb.flag2.Click += FlagGroupChange;
                                vb.flag3.Click += FlagGroupChange;
                                vb.flag4.Click += FlagGroupChange;
                                vb.flag5.Click += FlagGroupChange;
                                vb.flag6.Click += FlagGroupChange;
                                vb.flag7.Click += FlagGroupChange;
                                vb.flag8.Click += FlagGroupChange;
                                parentpanel.Children.Add(vb);
                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                            }
                            else if (tvd.ControlType == "FlagGroup")
                            {
                                if (!(entry.Value is TagLayouts.FlagGroup))
                                    continue;

                                TagLayouts.FlagGroup? fg = entry.Value as TagLayouts.FlagGroup;
                                TagFlagsGroup? vb = new();
                                vb.Tag = entry.Value.AbsoluteTagOffset;
                                vb.flag_name.Text = fg.N;
                                vb.generateBitsFromFile(fg.A, fg.MB, GetDataFromFile(fg.A, entry.Value.MemoryAddress), fg.STR);
                                vb.FlagToggled += FlagGroupChange;
                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                                parentpanel.Children.Add(vb);
                            }
                            else if (tvd.ControlType == "BoundsFloat")
                            {
                                tvd.Type = "Float";

                                TagTwoBlock? vb = new(30);
                                vb.f_name.Text = tvd.Name;
                                vb.f_label1.Text = "Min:";
                                vb.f_label2.Text = "Max:";
                                vb.f_value1.Text = BitConverter.ToSingle(GetDataFromFile(8, entry.Value.MemoryAddress), 0).ToString();
                                vb.f_value2.Text = BitConverter.ToSingle(GetDataFromFile(8, entry.Value.MemoryAddress), 4).ToString();
                                vb.f_value1.TextChanged += VBTextChange;
                                vb.f_value2.TextChanged += VBTextChange;
                                vb.f_value1.ToolTip = "Type: " + tvd.Type;
                                vb.f_value2.ToolTip = "Type: " + tvd.Type;
                                vb.f_value1.Tag = entry.Value.AbsoluteTagOffset;
                                vb.f_value2.Tag = entry.Value.AbsoluteTagOffset + "-4";
                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                                parentpanel.Children.Add(vb);

                            }
                            else if (tvd.ControlType == "Bounds2Byte")
                            {
                                tvd.Type = "2Byte";

                                TagTwoBlock? vb = new(30);
                                vb.f_name.Text = tvd.Name;
                                vb.f_label1.Text = "Min:";
                                vb.f_label2.Text = "Max:";
                                vb.f_value1.Text = BitConverter.ToInt16(GetDataFromFile(4, entry.Value.MemoryAddress), 0).ToString();
                                vb.f_value2.Text = BitConverter.ToInt16(GetDataFromFile(4, entry.Value.MemoryAddress), 2).ToString();
                                vb.f_value1.TextChanged += VBTextChange;
                                vb.f_value2.TextChanged += VBTextChange;
                                vb.f_value1.ToolTip = "Type: " + tvd.Type;
                                vb.f_value2.ToolTip = "Type: " + tvd.Type;
                                vb.f_value1.Tag = entry.Value.AbsoluteTagOffset;
                                vb.f_value2.Tag = entry.Value.AbsoluteTagOffset + "-2";
                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                                parentpanel.Children.Add(vb);
                            }
                            else if (tvd.ControlType == "2DPoint_Float")
                            {
                                tvd.Type = "Float";

                                TagTwoBlock? vb = new(13);
                                vb.f_name.Text = entry.Value.N;
                                vb.f_label1.Text = "X:";
                                vb.f_label2.Text = "Y:";
                                vb.f_value1.Text = BitConverter.ToSingle(GetDataFromFile(8, entry.Value.MemoryAddress), 0).ToString();
                                vb.f_value2.Text = BitConverter.ToSingle(GetDataFromFile(8, entry.Value.MemoryAddress), 4).ToString();
                                vb.f_value1.TextChanged += VBTextChange;
                                vb.f_value2.TextChanged += VBTextChange;
                                vb.f_value1.ToolTip = "Type: " + tvd.Type;
                                vb.f_value2.ToolTip = "Type: " + tvd.Type;
                                vb.f_value1.Tag = entry.Value.AbsoluteTagOffset;
                                vb.f_value2.Tag = entry.Value.AbsoluteTagOffset + "-4";
                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                                parentpanel.Children.Add(vb);
                            }
                            else if (tvd.ControlType == "2DPoint_2Byte")
                            {
                                tvd.Type = "2Byte";

                                TagTwoBlock? vb = new(13);
                                vb.f_name.Text = entry.Value.N;
                                vb.f_label1.Text = "X:";
                                vb.f_label2.Text = "Y:";
                                vb.f_value1.Text = BitConverter.ToInt16(GetDataFromFile(4, entry.Value.MemoryAddress), 0).ToString();
                                vb.f_value2.Text = BitConverter.ToInt16(GetDataFromFile(4, entry.Value.MemoryAddress), 2).ToString();
                                vb.f_value1.TextChanged += VBTextChange;
                                vb.f_value2.TextChanged += VBTextChange;
                                vb.f_value1.ToolTip = "Type: " + tvd.Type;
                                vb.f_value2.ToolTip = "Type: " + tvd.Type;
                                vb.f_value1.Tag = entry.Value.AbsoluteTagOffset;
                                vb.f_value2.Tag = entry.Value.AbsoluteTagOffset + "-2";
                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                                parentpanel.Children.Add(vb);
                            }
                            else if (tvd.ControlType == "3DPoint")
                            {
                                tvd.Type = "Float";

                                TagThreeBlock? vb = new(13);
                                vb.f_name.Text = entry.Value.N;
                                vb.f_label1.Text = "X:";
                                vb.f_label2.Text = "Y:";
                                vb.f_label3.Text = "Z:";
                                vb.f_value1.Text = BitConverter.ToSingle(GetDataFromFile(12, entry.Value.MemoryAddress), 0).ToString();
                                vb.f_value2.Text = BitConverter.ToSingle(GetDataFromFile(12, entry.Value.MemoryAddress), 4).ToString();
                                vb.f_value3.Text = BitConverter.ToSingle(GetDataFromFile(12, entry.Value.MemoryAddress), 8).ToString();
                                vb.f_value1.TextChanged += VBTextChange;
                                vb.f_value2.TextChanged += VBTextChange;
                                vb.f_value3.TextChanged += VBTextChange;
                                vb.f_value1.ToolTip = "Type: " + tvd.Type;
                                vb.f_value2.ToolTip = "Type: " + tvd.Type;
                                vb.f_value3.ToolTip = "Type: " + tvd.Type;
                                vb.f_value1.Tag = entry.Value.AbsoluteTagOffset;
                                vb.f_value2.Tag = entry.Value.AbsoluteTagOffset + "-4";
                                vb.f_value3.Tag = entry.Value.AbsoluteTagOffset + "-8";
                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                                parentpanel.Children.Add(vb);
                            }
                            else if (tvd.ControlType == "Quanternion")
                            {
                                tvd.Type = "Float";

                                TagFourBlock? vb = new(13);
                                vb.f_name.Text = entry.Value.N;
                                vb.f_label1.Text = "W:";
                                vb.f_label2.Text = "X:";
                                vb.f_label3.Text = "Y:";
                                vb.f_label4.Text = "Z:";
                                vb.f_value1.Text = BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 0).ToString();
                                vb.f_value2.Text = BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 4).ToString();
                                vb.f_value3.Text = BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 8).ToString();
                                vb.f_value4.Text = BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 12).ToString();
                                vb.f_value1.TextChanged += VBTextChange;
                                vb.f_value2.TextChanged += VBTextChange;
                                vb.f_value3.TextChanged += VBTextChange;
                                vb.f_value4.TextChanged += VBTextChange;
                                vb.f_value1.ToolTip = "Type: " + tvd.Type;
                                vb.f_value2.ToolTip = "Type: " + tvd.Type;
                                vb.f_value3.ToolTip = "Type: " + tvd.Type;
                                vb.f_value4.ToolTip = "Type: " + tvd.Type;
                                vb.f_value1.Tag = entry.Value.AbsoluteTagOffset;
                                vb.f_value2.Tag = entry.Value.AbsoluteTagOffset + "-4";
                                vb.f_value3.Tag = entry.Value.AbsoluteTagOffset + "-8";
                                vb.f_value4.Tag = entry.Value.AbsoluteTagOffset + "-12";
                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                                parentpanel.Children.Add(vb);
                            }
                            else if (tvd.ControlType == "3DPlane")
                            {
                                tvd.Type = "Float";

                                TagFourBlock? vb = new TagFourBlock(13);
                                vb.f_name.Text = entry.Value.N;
                                vb.f_label1.Text = "X:";
                                vb.f_label2.Text = "Y:";
                                vb.f_label3.Text = "Z:";
                                vb.f_label4.Text = "P:";
                                vb.f_value1.Text = BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 0).ToString();
                                vb.f_value2.Text = BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 4).ToString();
                                vb.f_value3.Text = BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 8).ToString();
                                vb.f_value4.Text = BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 12).ToString();
                                vb.f_value1.TextChanged += VBTextChange;
                                vb.f_value2.TextChanged += VBTextChange;
                                vb.f_value3.TextChanged += VBTextChange;
                                vb.f_value4.TextChanged += VBTextChange;
                                vb.f_value1.ToolTip = "Type: " + tvd.Type;
                                vb.f_value2.ToolTip = "Type: " + tvd.Type;
                                vb.f_value3.ToolTip = "Type: " + tvd.Type;
                                vb.f_value4.ToolTip = "Type: " + tvd.Type;
                                vb.f_value1.Tag = entry.Value.AbsoluteTagOffset;
                                vb.f_value2.Tag = entry.Value.AbsoluteTagOffset + "-4";
                                vb.f_value3.Tag = entry.Value.AbsoluteTagOffset + "-8";
                                vb.f_value4.Tag = entry.Value.AbsoluteTagOffset + "-12";
                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                                parentpanel.Children.Add(vb);
                            }
                            else if (tvd.ControlType == "RGB")
                            {
                                tvd.Type = "Float";

                                byte r_hex = (byte)(BitConverter.ToSingle(GetDataFromFile(12, entry.Value.MemoryAddress), 0) * 255);
                                byte g_hex = (byte)(BitConverter.ToSingle(GetDataFromFile(12, entry.Value.MemoryAddress), 4) * 255);
                                byte b_hex = (byte)(BitConverter.ToSingle(GetDataFromFile(12, entry.Value.MemoryAddress), 8) * 255);
                                string hex_color = r_hex.ToString("X2") + g_hex.ToString("X2") + b_hex.ToString("X2");

                                TagRGBBlock? vb = new();
                                vb.rgb_name.Text = entry.Value.N;
                                vb.color_hash.Text = "#" + hex_color;
                                vb.r_value.Text = BitConverter.ToSingle(GetDataFromFile(12, entry.Value.MemoryAddress), 0).ToString();
                                vb.g_value.Text = BitConverter.ToSingle(GetDataFromFile(12, entry.Value.MemoryAddress), 4).ToString();
                                vb.b_value.Text = BitConverter.ToSingle(GetDataFromFile(12, entry.Value.MemoryAddress), 8).ToString();
                                vb.rgb_colorpicker.SelectedColor = Color.FromRgb(r_hex, g_hex, b_hex);
                                vb.r_value.TextChanged += VBTextChange;
                                vb.g_value.TextChanged += VBTextChange;
                                vb.b_value.TextChanged += VBTextChange;
                                vb.r_value.Tag = entry.Value.AbsoluteTagOffset;
                                vb.g_value.Tag = entry.Value.AbsoluteTagOffset + "-4";
                                vb.b_value.Tag = entry.Value.AbsoluteTagOffset + "-8";
                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                                parentpanel.Children.Add(vb);
                            }
                            else if (tvd.ControlType == "ARGB")
                            {
                                tvd.Type = "Float";

                                byte a_hex2 = (byte)(BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 0) * 255);
                                byte r_hex2 = (byte)(BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 4) * 255);
                                byte g_hex2 = (byte)(BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 8) * 255);
                                byte b_hex2 = (byte)(BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 12) * 255);

                                TagARGBBlock? vb = new();
                                vb.rgb_name.Text = entry.Value.N;
                                vb.argb_colorpicker.SelectedColor = Color.FromArgb(a_hex2, r_hex2, g_hex2, b_hex2);
                                vb.a_value.Text = BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 0).ToString();
                                vb.r_value.Text = BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 4).ToString();
                                vb.g_value.Text = BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 8).ToString();
                                vb.b_value.Text = BitConverter.ToSingle(GetDataFromFile(16, entry.Value.MemoryAddress), 12).ToString();
                                vb.a_value.TextChanged += VBTextChange;
                                vb.r_value.TextChanged += VBTextChange;
                                vb.g_value.TextChanged += VBTextChange;
                                vb.b_value.TextChanged += VBTextChange;
                                vb.a_value.Tag = entry.Value.AbsoluteTagOffset;
                                vb.r_value.Tag = entry.Value.AbsoluteTagOffset + "-4";
                                vb.g_value.Tag = entry.Value.AbsoluteTagOffset + "-8";
                                vb.b_value.Tag = entry.Value.AbsoluteTagOffset + "-12";
                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                                parentpanel.Children.Add(vb);
                            }
                            else if (tvd.ControlType == "TagRef")
                            {
                                try
                                {
                                    byte[] data = GetDataFromFile((int)entry.Value.S, entry.Value.MemoryAddress);
                                    string tagId = Convert.ToHexString(GetDataFromFile(4, entry.Value.MemoryAddress + 8));
                                    string tagName = IDToTagName(tagId);
                                    byte[] tagGroupData = GetDataFromFile(4, entry.Value.MemoryAddress + 20);
                                    string tagGroup = "Null";

                                    TagInfo ti = GetTagInfo(tagId);
                                    string toolTip = "Tag ID: " + ti.TagID + "; Asset ID: " + ti.AssetID;
                                    if (Convert.ToHexString(tagGroupData) != "FFFFFFFF")
                                        tagGroup = ReverseString(Encoding.UTF8.GetString(tagGroupData));

                                    TagRefBlockFile? vb = new();
                                    vb.ToolTip = toolTip;
                                    vb.value_name.Text = entry.Value.N;
                                    vb.tag_button.Content = tagName;
                                    vb.taggroup.Text = tagGroup;
                                    vb.taggroup.IsReadOnly = true;
                                    tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                                    parentpanel.Children.Add(vb);
                                }
                                catch
                                {
                                    break;
                                }
                            }
                            else if (tvd.ControlType == "Tagblock")
                            {
                                int dataOffset = 0;
                                long stringAddress = BitConverter.ToInt64(GetDataFromFile(20, entry.Value.MemoryAddress), 8);
                                int childCount = BitConverter.ToInt32(GetDataFromFile(20, entry.Value.MemoryAddress), 16);
                                string childrenCount = childCount.ToString();
                                string our_name = "";
                                if (entry.Value.N != null)
                                    our_name = entry.Value.N;

                                long tempBlockAdd = 0;

                                if (tvd.DataBlockIndex != 0)
                                    tempBlockAdd = (long)moduleFile.Tag.DataBlockArray[tvd.DataBlockIndex].Offset - (long)moduleFile.Tag.DataBlockArray[0].Offset;

                                TagBlock? vb = new(tagStruct);
                                vb.tagblock_title.Text = our_name;
                                vb.BlockAddress = tempBlockAdd;
                                vb.tagblock_address.Text = "0x" + vb.BlockAddress.ToString("X");
                                vb.tagblock_address.IsReadOnly = true;
                                vb.Children = entry;
                                vb.tagblock_count.Text = childrenCount;
                                vb.tagblock_count.IsReadOnly = true;
                                vb.stored_num_on_index = 0;
                                vb.size = (int)entry.Value.S;

                                if (childCount > 0)
                                {
                                    vb.dataBlockInd = tvd.DataBlockIndex;
                                }
                                tagViewerControls.Add(entry.Value.AbsoluteTagOffset, vb);
                                parentpanel.Children.Add(vb);

                                if (childCount > 2000000)
                                {
                                    return;
                                }

                                if (entry.Value.B != null)
                                {
                                    List<string> indexBoxSource = new List<string>();
                                    for (int y = 0; y < childCount; y++)
                                    {
                                        indexBoxSource.Add(y.ToString());
                                    }
                                    vb.indexbox.ItemsSource = indexBoxSource;

                                    if (childCount > 0)
                                    {
                                        vb.indexbox.SelectedIndex = -1;
                                    }
                                    else
                                    {
                                        vb.Expand_Collapse_Button.IsEnabled = false;
                                        vb.Expand_Collapse_Button.Content = "";
                                        vb.indexbox.IsEnabled = false;
                                    }
                                }
                                else
                                {
                                    vb.Expand_Collapse_Button.IsEnabled = false;
                                    vb.Expand_Collapse_Button.Content = "";
                                    vb.indexbox.IsEnabled = false;

                                }
                            }

                            if (isTagBlock && tb != null)
                                tb.controlKeys.Add(entry.Value.AbsoluteTagOffset);

                            prevEntry = entry;
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusOut(ex.Message);
                    }
                }
            }
        }

        public void BuildTagBlock(IRTV_TagStruct tagStruct, KeyValuePair<long, TagLayouts.C> entry, TagBlock tagBlock, string absolute_address_chain)
        {
            tagBlock.dockpanel.Children.Clear();

            foreach (string key in tagBlock.controlKeys)
            {
                if (tagViewerControls.ContainsKey(key))
                    tagViewerControls.Remove(key);
            }

            tagBlock.controlKeys.Clear();

            if (entry.Value.B != null && tagBlock.dataBlockInd > 0)
            {
                try
                {
                    long newAddress = (long)moduleFile.Tag.DataBlockArray[tagBlock.dataBlockInd].Offset - (long)moduleFile.Tag.DataBlockArray[0].Offset + (entry.Value.S * tagBlock.stored_num_on_index);
                    long startingAddress = tagBlock.size * tagBlock.stored_num_on_index + newAddress;

                    CreateTagControls(tagStruct, startingAddress, entry.Value.B, newAddress, tagBlock.dockpanel, absolute_address_chain, tagBlock, true);
                }
                catch
                {
                    TextBox tb = new TextBox { Text = "this tagblock is fucked uwu" };
                    tagBlock.dockpanel.Children.Add(tb);
                }
            }
        }

        private byte[] GetDataFromFile(int size, long offset, ModuleFile? mf = null)
        {
            if (mf == null)
                mf = moduleFile;

            byte[] data = mf.Tag.TagData.Skip((int)offset).Take(((int)offset + size) - (int)offset).ToArray();
            return data;
        }

        private TagInfo GetTagInfo(string tagID)
        {
            TagInfo result = new TagInfo();

            if (inhaledTags.ContainsKey(tagID))
                result = inhaledTags[tagID];
            else
            {
                result.TagID = tagID;
                result.AssetID = "Unknown";
                result.TagPath = "Unknown";
            }

            return result;
        }
        #endregion

        #region Tag and Module Writing
        private void VBTextChange(object sender, TextChangedEventArgs e)
        {
            try
            {
                TextBox tb = (TextBox)sender;

                int writeOffset = 0;
                string dataKey = (string)tb.Tag;
                if (dataKey.Contains("-"))
                {
                    writeOffset = Convert.ToInt32(dataKey.Split("-")[1]);
                    dataKey = dataKey.Split("-")[0];
                }

                TagValueData tvd = tagValueData[dataKey];
                long totalOffset = Convert.ToInt64(tvd.Offset) + moduleFile.Tag.Header.HeaderSize;
                byte[] value = new byte[0];

                if (tvd.Type == "Byte")
                {
                    value = new byte[] { Convert.ToByte(tb.Text) };
                    tvd.Value = value;
                }
                else if (tvd.Type == "2Byte")
                {
                    value = BitConverter.GetBytes(Convert.ToInt16(tb.Text));
                    tvd.Value = value;
                }
                else if (tvd.Type == "4Byte")
                {
                    value = BitConverter.GetBytes(Convert.ToInt32(tb.Text));
                    tvd.Value = value;
                }
                else if (tvd.Type == "Float")
                {
                    value = BitConverter.GetBytes(Convert.ToSingle(tb.Text));
                    tvd.Value = value;
                }
                else if (tvd.Type == "mmr3Hash")
                {
                    if (tb.Text.Length == 8)
                    {
                        value = Convert.FromHexString(tb.Text);
                        tvd.Value = value;
                    }
                }
                else if (tvd.Type == "String")
                {
                    value = Encoding.ASCII.GetBytes(tb.Text);
                    tvd.Value = value;
                }

                if (value.Length > 0)
                {
                    if (tagStream != null)
                    {
                        tagStream.Position = totalOffset + writeOffset;
                        tagStream.Write(value);
                        Debug.WriteLine("Change at: " + totalOffset + " New Value: " + tb.Text + " Value Type: " + tvd.Type);
                    }
                    else if (tagFileStream != null)
                    {
                        tagFileStream.Position = totalOffset + writeOffset;
                        tagFileStream.Write(value);
                        Debug.WriteLine("Change at: " + totalOffset + " New Value: " + tb.Text + " Value Type: " + tvd.Type);
                    }
                }
            }
            catch
            {
                StatusOut("Error setting new value");
            }
        }

        public void FlagGroupChange(object sender, RoutedEventArgs e)
        {
            try
            {
                TagFlagsGroup tfg = (TagFlagsGroup)sender;
                TagValueData tvd = tagValueData[(string)tfg.Tag];

                long totalOffset = Convert.ToInt64(tvd.Offset) + moduleFile.Tag.Header.HeaderSize;
                byte[] value = tfg.data;
                tvd.Value = value;

                if (tagStream != null)
                {
                    tagStream.Position = totalOffset;
                    tagStream.Write(value);
                    Debug.WriteLine("Change at: " + totalOffset + " New Value: " + Convert.ToHexString(value) + " Value Type: " + tvd.Type);
                }
                else if (tagFileStream != null)
                {
                    tagFileStream.Position = totalOffset;
                    tagFileStream.Write(value);
                    Debug.WriteLine("Change at: " + totalOffset + " New Value: " + Convert.ToHexString(value) + " Value Type: " + tvd.Type);
                }
            }
            catch
            {
                StatusOut("Error setting new value");
            }
        }

        private void EnumBlockChanged(object sender, RoutedEventArgs e)
        {
            ComboBox cb = (ComboBox)sender;
            TagValueData tvd = tagValueData[(string)cb.Tag];
            byte[] data = BitConverter.GetBytes(cb.SelectedIndex);
            byte[] value = new byte[0];

            if (tvd.Size != null)
            {
                value = new byte[(int)tvd.Size];

                if (tvd.Size == 1)
                {
                    value[0] = data[0];
                }
                else if (tvd.Size == 2)
                {
                    value[0] = data[0];
                    value[1] = data[1];
                }
                else if (tvd.Size == 4)
                {
                    value[0] = data[0];
                    value[1] = data[1];
                    value[2] = data[2];
                    value[3] = data[3];
                }
            }


            long totalOffset = Convert.ToInt64(tvd.Offset) + moduleFile.Tag.Header.HeaderSize;
            tvd.Value = value;

            if (tagStream != null)
            {
                tagStream.Position = totalOffset;
                tagStream.Write(value);
                Debug.WriteLine("Change at: " + totalOffset + " New Value: " + Convert.ToHexString(value) + " Value Type: " + tvd.Type + " Size" + tvd.Size);
            }
            else if (tagFileStream != null)
            {
                tagFileStream.Position = totalOffset;
                tagFileStream.Write(value);
                Debug.WriteLine("Change at: " + totalOffset + " New Value: " + Convert.ToHexString(value) + " Value Type: " + tvd.Type + " Size" + tvd.Size);
            }
        }

        #endregion

        #region Hash Searching
        private void HashSearchClick(object sender, RoutedEventArgs e)
        {
            StatusOut("Searching...");
            ResetHashSearch();
            string search = hashSeachBox.Text.Trim();
            if (search.Length == 8)
            {
                string foundLine = "";
                int curLine = 1;
                foreach (string line in File.ReadAllLines(@".\Files\mmr3Hashes.txt"))
                {
                    if (line.Contains(search))
                    {
                        foundLine = line;
                        break;
                    }
                    curLine++;
                }

                if (foundLine.Length > 1)
                {
                    StatusOut("Hash found at line: " + curLine);
                    string hash = foundLine.Split("~")[0].Split("-")[1];
                    string hashName = foundLine.Split("~")[1].Split("-")[1];
                    string TagList = foundLine.Split("~")[2].Replace("Tags{", "").Replace("}", "");
                    string[] Tags = TagList.Split("],");

                    HashBox.Text = hash;
                    HashNameBox.Text = hashName;
                    TagCountBox.Text = Tags.Count().ToString();

                    foreach (string tag in Tags)
                    {
                        string tagID = ReverseHexString(tag.Split("[").First());
                        string tagName = "Object ID: " + tagID;
                        if (inhaledTags.ContainsKey(tagID))
                            tagName = inhaledTags[tagID].TagPath.Split("\\").Last();

                        TreeViewItem item = new TreeViewItem();
                        item.Header = tagName;
                        item.Selected += HashTagSelected;
                        item.Tag = tag;
                        HashTree.Items.Add(item);
                    }
                }
            }
            else
            {
                StatusOut("Incorrect hash format!");
            }

        }

        private void HashTagSelected(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)sender;
            string tagID = ReverseHexString(((string)item.Tag).Split("[").First());
            string tagName = "Object ID: " + tagID;
            string module = "";
            if (inhaledTags.ContainsKey(tagID))
            {
                tagName = inhaledTags[tagID].TagPath.Split("\\").Last();
                module = inhaledTags[tagID].ModulePath;
            }

            TagIDBox.Text = tagID;
            TagNameBox.Text = tagName;
            ModuleNameBox.Text = module;

            string referenceList = ((string)item.Tag).Split("[").Last().Replace("]", "");
            string[] references = referenceList.Split(",");

            ReferencePanel.Children.Clear();
            foreach (string reference in references)
            {
                HashReference hr = new();
                hr.ValueName.Text = reference.Split(":")[0];
                hr.ValueOffset.Text = reference.Split(":")[1];
                ReferencePanel.Children.Add(hr);
            }
        }

        private void ResetHashSearch()
        {
            HashBox.Text = "";
            HashNameBox.Text = "";
            TagCountBox.Text = "";
            HashTree.Items.Clear();
            TagIDBox.Text = "";
            TagNameBox.Text = "";
            ModuleNameBox.Text = "";
            ReferencePanel.Children.Clear();
        }
        #endregion

        #region Data Extraction
        private void ExtractClick(object sender, RoutedEventArgs e)
        {
            if (DumpTypeCB.SelectedIndex == 0)
            {
                Dictionary<string, TagInfo> tInfo = TagInfoExport.ExtractTagInfo(modulePaths);

                SaveFileDialog sfd = new();
                sfd.Filter = "Json (*.json)|*.json";
                sfd.FileName = "tagInfo.json";
                if (sfd.ShowDialog() == true)
                {
                    File.WriteAllText(sfd.FileName, JsonConvert.SerializeObject(tInfo, Formatting.Indented).ToString());
                }

                StatusOut("Retrieving tag info...");
            }

            if (DumpTypeCB.SelectedIndex == 1)
            {
                StatusOut("Attempting to export tag to JSON...");
                if (tagFileName.Length > 0 && curTagID.Length == 8)
                {
                    string? result = JsonExport.ExportTagToJson(TagLayouts.Tags(inhaledTags[curTagID].TagGroup), GetTagInfo(curTagID), moduleFile);

                    if (result != null)
                    {
                        StatusOut("Tag data converted to JSON!");
                        // Save File
                        SaveFileDialog sfd = new();
                        sfd.Filter = "Json (*.json)|*.json";
                        sfd.FileName = tagFileName.Split("\\").Last().Split(".").First() + ".json";
                        if (sfd.ShowDialog() == true)
                        {
                            File.WriteAllText(sfd.FileName, result);
                        }

                        StatusOut("Tag successfully exported to JSON!");
                        return;
                    }
                }

                StatusOut("Error exporting tag to json!");
            }

            if (DumpTypeCB.SelectedIndex == 2)
            {
                StatusOut("Attempting to export material data...");
                if (tagFileName.Length > 0 && curTagID.Length == 8 && moduleStream != null && tagStream != null && module != null)
                {
                    // Close tag
                    tagStream.Close();

                    // Attempt to export material data
                    string? result = MaterialExport.ExportMaterial(GetTagInfo(curTagID), moduleStream, module);

                    if (result != null)
                    {
                        // Save File
                        StatusOut("Material data converted to JSON!");
                        SaveFileDialog sfd = new();
                        sfd.Filter = "Json (*.json)|*.json";
                        sfd.FileName = tagFileName.Split("\\").Last().Split(".").First() + ".json";
                        if (sfd.ShowDialog() == true)
                        {
                            File.WriteAllText(sfd.FileName, result);
                        }

                        StatusOut("Material successfully exported to JSON!");
                        return;
                    }

                    // Re-open tag
                    tagStream = ModuleEditor.GetTag(module, moduleStream, tagFileName);
                }

                StatusOut("Error exporting material data!");
            }

            if (DumpTypeCB.SelectedIndex == 3)
            {
                StatusOut("Attempting to export forge data...");
                // Attempt to export forge data

                string? result = ForgeExport.ExtractForgeData(modulePaths);

                if (result != null)
                {
                    // Save File
                    StatusOut("Forge data converted to JSON!");
                    SaveFileDialog sfd = new();
                    sfd.Filter = "Json (*.json)|*.json";
                    sfd.FileName = "ForgeObjectData.json";
                    if (sfd.ShowDialog() == true)
                    {
                        File.WriteAllText(sfd.FileName, result);
                    }

                    StatusOut("Forge data successfully exported to JSON!");
                }
                else
                {
                    StatusOut("Error retrieving forge data!");
                }
            }

            if (DumpTypeCB.SelectedIndex == 4)
            {
                StatusOut("Attempting to export hashes...");
                List<string> result = HashExport.DumpHashes(modulePaths);

                if (result.Count > 0)
                {
                    // Save File
                    StatusOut("Hashes found!");
                    SaveFileDialog sfd = new();
                    sfd.Filter = "Text (*.txt)|*.txt";
                    sfd.FileName = "mmr3Hashes.txt";
                    if (sfd.ShowDialog() == true)
                    {
                        File.WriteAllLines(sfd.FileName, result);
                    }
                    StatusOut("Hashes exported!");
                }
                else
                {
                    StatusOut("Error retrieving hashes!");
                }
            }
        }

        private void DumpTypeChange(object sender, RoutedEventArgs e)
        {
            if (DumpTypeCB.SelectedIndex == 0)
            {
                FormatOption.Visibility = Visibility.Visible;
                FlatFileOption.Visibility = Visibility.Visible;
            }
            else
            {
                FormatOption.Visibility = Visibility.Collapsed;
                FlatFileOption.Visibility = Visibility.Collapsed;
            }
        }
        #endregion
    }
}
