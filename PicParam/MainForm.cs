﻿#region Using directives
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;

using Pic.DAL.SQLite;
using Pic.Plugin;
using Pic.Factory2D;
using DesLib4NET;
using GLib.Options;

using log4net;
using PicParam.Properties;

using TreeDim.StackBuilder.GUIExtension;
#endregion

namespace PicParam
{
    public partial class MainForm : Form
    {
        #region Constructor
        public MainForm()
        {
            InitializeComponent();
            this.Text = Application.ProductName;

            _startPageCtrl.TreeViewCtrl = _treeViewCtrl;

            // set export application
            ApplicationAvailabilityChecker.AppendApplication("PicGEOM", Pic.DAL.ApplicationConfiguration.CustomSection.AppPicGEOM);
            ApplicationAvailabilityChecker.AppendApplication("PicDecoupe", Pic.DAL.ApplicationConfiguration.CustomSection.AppPicdecoup);
            ApplicationAvailabilityChecker.AppendApplication("Picador3D", Pic.DAL.ApplicationConfiguration.CustomSection.AppPicador3D);

            _pluginViewCtrl.DependancyStatusChanged += new Pic.Plugin.ViewCtrl.PluginViewCtrl.DependancyStatusChangedHandler(DependancyStatusChanged);
        }
        #endregion

        #region Overrides
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _splitContainer.SplitterDistance = 200;
        }

        /// <summary>
        /// handle Ctrl+Z key
        /// </summary>
        /// <returns>true if key is correctly handled</returns> 
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Z:
                    if (_factoryViewCtrl.Visible) _factoryViewCtrl.FitView();
                    if (_pluginViewCtrl.Visible) _pluginViewCtrl.FitView();
                    return true;
                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
        }
        #endregion

        #region Control event handlers
        private void BranchViewBranchSelected(object sender, NodeEventArgs e)
        {
            // get selected tree node
            _treeViewCtrl.PopulateAndSelectNode(new NodeTag(NodeTag.NodeType.NT_TREENODE, e.Node));
        }

        private void LoadParametricComponent(string filePath)
        {
            _pluginViewCtrl.Visible = true;

            _pluginViewCtrl.PluginPath = filePath;

            toolStripButtonCotations.Enabled = true;
            toolStripButtonReflectionX.Enabled = true;
            toolStripButtonReflectionY.Enabled = true;
            toolStripButtonExport.Enabled = true;
            exportToolStripMenuItem.Enabled = true;
            cotationsToolStripMenuItem.Enabled = true;
        }

        private void LoadPicadorFile(string filePath, string fileFormat)
        {
            _factoryViewCtrl.Visible = true;

            PicFactory factory = _factoryViewCtrl.Factory;

            if (string.Equals("des", fileFormat, StringComparison.CurrentCultureIgnoreCase))
            {
                PicLoaderDes picLoaderDes = new PicLoaderDes(factory);
                using (DES_FileReader fileReader = new DES_FileReader())
                    fileReader.ReadFile(filePath, picLoaderDes);
                // remove existing quotations
                factory.Remove((new PicFilterCode(PicEntity.eCode.PE_COTATIONDISTANCE))
                                    | (new PicFilterCode(PicEntity.eCode.PE_COTATIONHORIZONTAL))
                                    | (new PicFilterCode(PicEntity.eCode.PE_COTATIONVERTICAL)));
                // build autoquotation
                PicAutoQuotation.BuildQuotation(factory);
            }
            else if (string.Equals("dxf", fileFormat, StringComparison.CurrentCultureIgnoreCase))
            {
                using (PicLoaderDxf picLoaderDxf = new PicLoaderDxf(factory))
                {
                    picLoaderDxf.Load(filePath);
                    picLoaderDxf.FillFactory();
                }
            }
            // update _factoryViewCtrl
            _factoryViewCtrl.FitView();
        }

        private void LoadPdfWithActiveX(string filePath)
        {
            _webBrowser4PDF.Url = new Uri(filePath);
            _webBrowser4PDF.Size = this._splitContainer.Panel2.Size;
            _webBrowser4PDF.Visible = true;
        }

        private void LoadImageFile(string filePath)
        {
            _webBrowser4PDF.Url = new Uri(filePath);
            _webBrowser4PDF.Size = this._splitContainer.Panel2.Size;
            _webBrowser4PDF.Visible = true;
        }

        private void LoadUnknownFileFrame(string filePath)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.Verb = "Open";
            startInfo.CreateNoWindow = false;
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
            startInfo.FileName = filePath;
            using (Process p = new Process())
            {
                p.StartInfo = startInfo;
                p.Start();
            }
        }

        Pic.DAL.SQLite.Document GetSelectedDocument()
        {
            NodeTag nodeTag = _treeViewCtrl.SelectedNode.Tag as NodeTag;
            if (null == nodeTag)
                return null;

            PPDataContext db = new PPDataContext();
            Pic.DAL.SQLite.TreeNode treeNode = Pic.DAL.SQLite.TreeNode.GetById(db, nodeTag.TreeNode);
            return treeNode.Documents(db)[0];
        }

        void DependancyStatusChanged(bool hasDependancy)
        {
            toolStripButtonEditParameters.Enabled = hasDependancy && _pluginViewCtrl.Visible;
        }
        #endregion

        #region Menu event handlers
        #region File
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        #endregion
        #region Help
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBoxForm form = new AboutBoxForm();
            form.ShowDialog();
        }
        #endregion
        private void editProfilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FormEditProfiles form = new FormEditProfiles();
            form.ShowDialog();
        }
        #region Database
        /// <summary>
        /// Backup
        /// </summary>
        private void toolStripMenuItemBackup_Click(object sender, EventArgs e)
        {
            if (DialogResult.OK == saveFileDialogBackup.ShowDialog())
                FormWorkerThreadTask.Execute(new Pic.DAL.TPTBackup(saveFileDialogBackup.FileName));
        }
        /// <summary>
        /// Restore
        /// </summary>
        private void toolStripMenuItemRestore_Click(object sender, EventArgs e)
        {
            // warning
            if (MessageBox.Show(PicParam.Properties.Resources.ID_RESTOREWARNING
                , ProductName
                , MessageBoxButtons.OKCancel
                , MessageBoxIcon.Warning
                ) == DialogResult.Cancel)
                return;
            if (DialogResult.OK == openFileDialogRestore.ShowDialog())
            {
                FormWorkerThreadTask.Execute(new Pic.DAL.TPTRestore(openFileDialogRestore.FileName));
                // refresh tree
                _treeViewCtrl.RefreshTree();
                // expand all so that modifications can be visualized
                if (_treeViewCtrl.Nodes.Count > 0)
                    _treeViewCtrl.Nodes[0].ExpandAll();
            }
        }
        /// <summary>
        /// Merge
        /// </summary>
        private void toolStripMenuItemMerge_Click(object sender, EventArgs e)
        {
            if (DialogResult.OK == openFileDialogRestore.ShowDialog())
            {
                FormWorkerThreadTask.Execute(new Pic.DAL.TPTMerge(openFileDialogRestore.FileName));
                // refresh tree
                _treeViewCtrl.RefreshTree();
                // back to default cursor
                Cursor.Current = Cursors.Default;
            }
        }
        /// <summary>
        /// Clear
        /// </summary>
        private void toolStripMenuItemClearDatabase_Click(object sender, EventArgs e)
        {
            try
            {
                // form
                if (DialogResult.Yes == MessageBox.Show(PicParam.Properties.Resources.ID_CLEARDATABASEWARNING))
                {
                    // start thread
                    FormWorkerThreadTask.Execute(new Pic.DAL.TPTClearDatabase());
                    // refresh tree
                    _treeViewCtrl.RefreshTree();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(ex.Message));
            }
        }
        #endregion
        private void toolStripMenuItemBrowseFile_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog fd = new OpenFileDialog();
                fd.Filter = "Picador file|*.des|Autocad dxf|*.dxf|All Files|*.*";
                if (DialogResult.OK == fd.ShowDialog())
                {
                    FormBrowseFile form = new FormBrowseFile(fd.FileName);
                    form.ShowDialog();
                }
            }
            catch (Exception ex)
            {
				Debug.Fail(ex.ToString());
				_log.Error(ex.ToString());                
            }
        }

        private void customizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                // show option form
                OptionsFormPLMPackLib form = new OptionsFormPLMPackLib();
                DialogResult dres = form.ShowDialog();
                if (DialogResult.OK == dres)
                {
                    // need to force saving of Pic.Factory2D.Properties.Settings
                    Pic.Factory2D.Properties.Settings.Default.Save();
                    // if need to restart application, indicate the user that the application will need to restart before exiting
                    if (form.ApplicationMustRestart)
                    {
                        MessageBox.Show(string.Format(Properties.Resources.ID_APPLICATIONMUSTRESTART, Application.ProductName));
                        Application.Exit();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.ToString());
                _log.Error(ex.ToString());
            }
        }
        #endregion

        #region Export toolbar event handler
        void toolStripButtonExport_Click(object sender, System.EventArgs e)
        {
            try
            {
                FormExportFile form = new FormExportFile();
                form.FileName = DocumentName;
                if (DialogResult.OK == form.ShowDialog())
                {
                    if (_pluginViewCtrl.Visible)
                        _pluginViewCtrl.WriteExportFile(form.FilePath, form.ActualFileExtension);
                    else if (_factoryViewCtrl.Visible)
                        _factoryViewCtrl.WriteExportFile(form.FilePath, form.ActualFileExtension);
                    if (form.OpenFile)
                    {
                        using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
                        {
                            if ("des" == form.ActualFileExtension)
                            {
                                proc.StartInfo.FileName = Pic.DAL.ApplicationConfiguration.CustomSection.AppPicGEOM;
                                proc.StartInfo.Arguments = "\"" + form.FilePath + "\"";
                            }
                            else if ("dxf" == form.ActualFileExtension)
                            {
                                proc.StartInfo.FileName = Pic.DAL.ApplicationConfiguration.CustomSection.ApplicationDxf;
                                proc.StartInfo.Arguments = "\"" + form.FilePath + "\"";
                            }
                            else if ("pdf" == form.ActualFileExtension)
                            {
                                proc.StartInfo.FileName = form.FilePath;
                                // actually using shell execute
                                proc.StartInfo.UseShellExecute = true;
                                proc.StartInfo.Verb = "open";
                            }
                            // checking if called application can be found
                            if (!proc.StartInfo.UseShellExecute && !System.IO.File.Exists(proc.StartInfo.FileName))
                            {
                                MessageBox.Show(string.Format("Application {0} could not be found!"
                                    , proc.StartInfo.FileName)
                                    , Application.ProductName
                                    , MessageBoxButtons.OK
                                    , MessageBoxIcon.Error);
                                return;
                            }
                            proc.Start();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.ToString());
                _log.Error(ex.ToString());
            }
        }

        private void toolStripButtonPicGEOM_Click(object sender, EventArgs e)
        {
            ExportAndOpen("des", Pic.DAL.ApplicationConfiguration.CustomSection.AppPicGEOM);
        }
        private void toolStripButtonPicDecoup_Click(object sender, EventArgs e)
        {
            ExportAndOpen("des", Pic.DAL.ApplicationConfiguration.CustomSection.AppPicdecoup);
        }
        private void toolStripButtonPicador3D_Click(object sender, EventArgs e) 
        {
            ExportAndOpen("des", Pic.DAL.ApplicationConfiguration.CustomSection.AppPicador3D);
        }
        private void toolStripButtonDXF_Click(object sender, EventArgs e)
        {
            if (!System.IO.File.Exists(Pic.DAL.ApplicationConfiguration.CustomSection.ApplicationDxf))
            {
                OpenFileDialog fd = new OpenFileDialog();
                fd.InitialDirectory = Environment.CurrentDirectory;
                fd.RestoreDirectory = true;
                fd.Filter = "Executable (*.exe)|*.exe|All Files|*.*";
                fd.FilterIndex = 1;
                fd.Multiselect = false;
                fd.CheckFileExists = true;
                if (DialogResult.OK == fd.ShowDialog())
                    Pic.DAL.ApplicationConfiguration.CustomSection.ApplicationDxf = fd.FileName;
                else
                    return;
            }
            ExportAndOpen("dxf", Pic.DAL.ApplicationConfiguration.CustomSection.ApplicationDxf);
        }
        private void ExportAndOpen(string sExt, string sPathExectable)
        {
            try
            {
                // build temp file path
                string tempFilePath = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), sExt);
                // write file
                if (_pluginViewCtrl.Visible)
                    _pluginViewCtrl.WriteExportFile(tempFilePath, sExt);
                else if (_factoryViewCtrl.Visible)
                    _factoryViewCtrl.WriteExportFile(tempFilePath, sExt);
                // open using existing file path
                using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
                {
                    proc.StartInfo.FileName = sPathExectable;
                    proc.StartInfo.Arguments = "\"" + tempFilePath + "\"";
                    proc.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
            }        
        
        }
        #endregion

        #region Toolbar event handlers
        void toolStripButtonCotations_Click(object sender, System.EventArgs e)
        {
            if (_pluginViewCtrl.Visible)
                _pluginViewCtrl.ShowCotations = !_pluginViewCtrl.ShowCotations;
            else if (_factoryViewCtrl.Visible)
                _factoryViewCtrl.ShowCotations = !_factoryViewCtrl.ShowCotations;
            UpdateToolCommands();
        }
        private void toolStripButtonReflectionX_Click(object sender, EventArgs e)
        {
            if (_pluginViewCtrl.Visible)
                _pluginViewCtrl.ReflectionX = !_pluginViewCtrl.ReflectionX;
            else if (_factoryViewCtrl.Visible)
                _factoryViewCtrl.ReflectionX = !_factoryViewCtrl.ReflectionX;
            UpdateToolCommands();
        }
        private void toolStripButtonReflectionY_Click(object sender, EventArgs e)
        {
            if (_pluginViewCtrl.Visible)
                _pluginViewCtrl.ReflectionY = !_pluginViewCtrl.ReflectionY;
            else if (_factoryViewCtrl.Visible)
                _factoryViewCtrl.ReflectionY = !_factoryViewCtrl.ReflectionY;
            UpdateToolCommands();
        }
         private void toolStripButtonLayout_Click(object sender, EventArgs e)
        {
            try
            {
                if (_pluginViewCtrl.Visible)
                    _pluginViewCtrl.BuildLayout();
                else if (_factoryViewCtrl.Visible)
                    _factoryViewCtrl.BuildLayout();
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.ToString());
                _log.Error(ex.ToString());
            }
        }
        private void UpdateToolCommands()
        {
            try
            {
                // enable toolbar buttons
                bool buttonsEnabled = _pluginViewCtrl.Visible || _factoryViewCtrl.Visible;
                toolStripButtonCotations.Enabled = buttonsEnabled;
                cotationsToolStripMenuItem.Enabled = buttonsEnabled;
                toolStripButtonReflectionX.Enabled = buttonsEnabled;
                reflectionXToolStripMenuItem.Enabled = buttonsEnabled;
                toolStripButtonReflectionY.Enabled = buttonsEnabled;
                reflectionYToolStripMenuItem.Enabled = buttonsEnabled;
                toolStripButtonLayout.Enabled = buttonsEnabled;
                layoutToolStripMenuItem.Enabled = buttonsEnabled;
                cotationShortLinesToolStripMenuItem.Enabled = buttonsEnabled;
                // only allow palletization / case optimisation when a component is selected
                toolStripButtonPalletization.Enabled = _pluginViewCtrl.Visible && _pluginViewCtrl.AllowPalletization;
                toolStripButtonCaseOptimization.Enabled = _pluginViewCtrl.Visible && _pluginViewCtrl.AllowPalletization;
                // enable export toolbar buttons
                toolStripButtonExport.Enabled = buttonsEnabled;
                toolStripButtonPicGEOM.Enabled = buttonsEnabled && ApplicationAvailabilityChecker.IsAvailable("PicGEOM");
                toolStripButtonPicDecoup.Enabled = buttonsEnabled && ApplicationAvailabilityChecker.IsAvailable("PicDecoup");
                toolStripButtonPicador3D.Enabled = buttonsEnabled && ApplicationAvailabilityChecker.IsAvailable("Picador3D"); 
                toolStripButtonDXF.Enabled = buttonsEnabled;
                // "File" menu items
                exportToolStripMenuItem.Enabled = buttonsEnabled;
                toolStripMenuItemPicGEOM.Enabled = buttonsEnabled && ApplicationAvailabilityChecker.IsAvailable("PicGEOM");

                // check state
                bool showCotations = false, reflectionX = false, reflectionY = false;
                if (_pluginViewCtrl.Visible)
                {
                    showCotations = _pluginViewCtrl.ShowCotations;
                    reflectionX = _pluginViewCtrl.ReflectionX;
                    reflectionY = _pluginViewCtrl.ReflectionY;
                }
                else if (_factoryViewCtrl.Visible)
                {
                    showCotations = _factoryViewCtrl.ShowCotations;
                    reflectionX = _factoryViewCtrl.ReflectionX;
                    reflectionY = _factoryViewCtrl.ReflectionY;
                }

                toolStripButtonCotations.CheckState = showCotations ? CheckState.Checked : CheckState.Unchecked;
                cotationsToolStripMenuItem.CheckState = showCotations ? CheckState.Checked : CheckState.Unchecked;
                toolStripButtonReflectionX.CheckState = reflectionX ? CheckState.Checked : CheckState.Unchecked;
                reflectionXToolStripMenuItem.CheckState = reflectionX ? CheckState.Checked : CheckState.Unchecked;
                toolStripButtonReflectionY.CheckState = reflectionY ? CheckState.Checked : CheckState.Unchecked;
                reflectionYToolStripMenuItem.CheckState = reflectionY ? CheckState.Checked : CheckState.Unchecked;
                toolStripEditComponentCode.Enabled = _pluginViewCtrl.Visible;

                toolStripButtonEditParameters.Enabled = _pluginViewCtrl.Visible && _pluginViewCtrl.HasDependancies;
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
            }
        }
        private void toolStripEditComponentCode_Click(object sender, EventArgs e)
        {
            try
            {
                NodeTag nodeTag = _treeViewCtrl.SelectedNode.Tag as NodeTag;
                if (null == nodeTag) return;
                PPDataContext db = new PPDataContext();
                Pic.DAL.SQLite.TreeNode treeNode = Pic.DAL.SQLite.TreeNode.GetById(db, nodeTag.TreeNode);
                if (null == treeNode) return;
                Pic.DAL.SQLite.Document doc = treeNode.Documents(db)[0];
                if (null == doc) return;
                Pic.DAL.SQLite.Component comp = doc.Components[0];
                if (null == comp) return;
                // output Guid / path
                Guid outputGuid = Guid.NewGuid();
                string outputPath = Pic.DAL.SQLite.File.GuidToPath(db, outputGuid, "dll");
                // form plugin editor
                FormPluginEditor editorForm = new FormPluginEditor();
                editorForm.PluginPath = doc.File.Path(db);
                editorForm.OutputPath = outputPath;
                if (DialogResult.OK == editorForm.ShowDialog())
                {
                    _log.Info("Component successfully modified!");
                    doc.File.Guid = outputGuid;
                    db.SubmitChanges();
                    // clear component cache in plugin viewer
                    ComponentLoader.ClearCache();
                    // update pluginviewer
                    _pluginViewCtrl.PluginPath = outputPath;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
            }
        }

        private void toolStripButtonEditParameters_Click(object sender, EventArgs e)
        {
            try
            {
                FormEditDefaultParamValues form = new FormEditDefaultParamValues( _pluginViewCtrl.Dependancies );
                if (DialogResult.OK == form.ShowDialog())
                {   // also see on Ok button handler
                    // clear plugin loader cache
                    ComponentLoader.ClearCache();

                    if (_pluginViewCtrl.Visible)
                        _pluginViewCtrl.Refresh();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
            }
        }
        private void cotationShortLinesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // update static flag
            PicGlobalCotationProperties.ShowShortCotationLines = !PicGlobalCotationProperties.ShowShortCotationLines;
            _log.Info(string.Format("Switched cotation short lines. New value : {0}", PicGlobalCotationProperties.ShowShortCotationLines.ToString()));
            // update menu
            cotationShortLinesToolStripMenuItem.Checked = PicGlobalCotationProperties.ShowShortCotationLines;
            // save setting
            PicParam.Properties.Settings.Default.UseCotationShortLines = PicGlobalCotationProperties.ShowShortCotationLines;
        }
        private void toolStripButtonRoot_Click(object sender, EventArgs e)
        {
            try
            {
                _treeViewCtrl.CollapseRootChildrens();                
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
            }
        }
        private void defineDatabasePathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                FormEditDatabasePath form = new FormEditDatabasePath();
                form.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
            }
        }
        private void toolStripButtonHelp_Click(object sender, EventArgs e)
        {

        }
        #endregion

        #region Palletization toolbar event handlers
        private void toolStripButtonPalletization_Click(object sender, EventArgs e)
        {
            try
            {
                double length = 0.0, width = 0.0, height = 0.0;
                if (_pluginViewCtrl.GetDimensions(ref length, ref width, ref height))
                {
                    TreeDim.StackBuilder.GUIExtension.Palletization palletization = new Palletization();
                    palletization.StartPalletization(_pluginViewCtrl.LoadedComponentName, length, width, height);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
            }
        }

        private void toolStripButtonCaseOptimization_Click(object sender, EventArgs e)
        {
            try
            {
                double length = 0.0, width = 0.0, height = 0.0;
                if (_pluginViewCtrl.GetDimensions(ref length, ref width, ref height))
                {
                    TreeDim.StackBuilder.GUIExtension.Palletization palletization = new Palletization();
                    palletization.StartCaseOptimization(_pluginViewCtrl.LoadedComponentName, length, width, height);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
            }            
        }
        #endregion

        #region Private properties
        private string DocumentName
        {
            get { return _docName; }
            set { _docName = value; }
        }
        #endregion

        #region MainForm Load/Close event handling
        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                // load settings
                ToolStripManager.LoadSettings(this, this.Name);

                // --- instantiate and start splach screen thread
                // cardboard format loader
                CardboardFormatLoader formatLoader = new CardboardFormatLoaderImpl();
                _pluginViewCtrl.CardboardFormatLoader = formatLoader;
                _factoryViewCtrl.CardBoardFormatLoader = formatLoader;

                // profile loader
                _profileLoaderImpl = new ProfileLoaderImpl();
                _pluginViewCtrl.ProfileLoader = _profileLoaderImpl;
                // search method
                ComponentSearchMethodDB searchmethod = new ComponentSearchMethodDB();
                _pluginViewCtrl.SearchMethod = new ComponentSearchMethodDB();

                _treeViewCtrl.StartPageSelected += new DocumentTreeView.StartPageSelectHandler(ShowStartPage);

                _treeViewCtrl.SelectionChanged += new DocumentTreeView.SelectionChangedHandler(_branchViewCtrl.OnSelectionChanged);
                _treeViewCtrl.SelectionChanged += new DocumentTreeView.SelectionChangedHandler(OnSelectionChanged);

                _branchViewCtrl.SelectionChanged += new DocumentTreeBranchView.SelectionChangedHandler(_treeViewCtrl.OnSelectionChanged);
                _branchViewCtrl.SelectionChanged += new DocumentTreeBranchView.SelectionChangedHandler(OnSelectionChanged);
                _branchViewCtrl.TreeNodeCreated += new DocumentTreeBranchView.TreeNodeCreatedHandler(_treeViewCtrl.OnTreeNodeCreated);

                // ---
                // initialize menu state
                PicGlobalCotationProperties.ShowShortCotationLines = PicParam.Properties.Settings.Default.UseCotationShortLines;
                _log.Info(string.Format("ShowShortCotationLines initialized with value : {0}", PicParam.Properties.Settings.Default.UseCotationShortLines.ToString()));
                cotationShortLinesToolStripMenuItem.Checked = PicGlobalCotationProperties.ShowShortCotationLines;

                // show start page
                ShowStartPage(this);
            }
            catch (System.Exception ex)
            {
                Debug.Fail(ex.ToString());
                _log.Error(ex.ToString());
            }
            // restore window position
            if (null != Settings.Default.MainFormSettings && !Settings.Default.StartMaximized)
            {
                Settings.Default.MainFormSettings.Restore(this);
            }
            // show maximized
            if (Settings.Default.StartMaximized)
                WindowState = FormWindowState.Maximized;
        }

        private void OnSelectionChanged(object sender, NodeEventArgs e, string name)
        {
            // changed caption
            Text = Application.ProductName + " - " + name;

            // show/hide controls
            _startPageCtrl.Visible      = false;
            _branchViewCtrl.Visible     = (NodeTag.NodeType.NT_TREENODE == e.Type);
            _pluginViewCtrl.Visible     = false;
            _factoryViewCtrl.Visible    = false;
            _webBrowser4PDF.Visible     = false;
            if (NodeTag.NodeType.NT_DOCUMENT == e.Type)
            {
                PPDataContext db = new PPDataContext();
                Pic.DAL.SQLite.TreeNode treeNode = Pic.DAL.SQLite.TreeNode.GetById(db, e.Node);
                Document doc = treeNode.Documents(db)[0];

                DocumentName = doc.Name;

                // select document handler depending on document type
                string docTypeName = doc.DocumentType.Name;
                string filePath = doc.File.Path(db);
                if (string.Equals("Parametric Component", docTypeName, StringComparison.CurrentCultureIgnoreCase))
                {
                    if (doc.Components.Count > 0)
                        _profileLoaderImpl.Component = doc.Components[0];
                    LoadParametricComponent(filePath);
                }
                else if (string.Equals("treeDim des", docTypeName, StringComparison.CurrentCultureIgnoreCase))
                    LoadPicadorFile(filePath, "des");
                else if (string.Equals("autodesk dxf", docTypeName, StringComparison.CurrentCultureIgnoreCase))
                    LoadPicadorFile(filePath, "dxf");
                else if (string.Equals("Adobe Acrobat", docTypeName, StringComparison.CurrentCultureIgnoreCase))
                    LoadPdfWithActiveX(filePath);
                else if (string.Equals("raster image", docTypeName, StringComparison.CurrentCultureIgnoreCase))
                    LoadImageFile(filePath);
                else
                    LoadUnknownFileFrame(filePath);
            }
            // update toolbar
            UpdateToolCommands();
            // select treeview control
            _treeViewCtrl.Select();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // save toolstrip settings
            ToolStripManager.SaveSettings(this, this.Name);
            // do not save window position if StartMaximized property is set
            if (Settings.Default.StartMaximized) return;
            // save window position
            if (null == Settings.Default.MainFormSettings)
            {
                Settings.Default.MainFormSettings = new WindowSettings();
            }
            Settings.Default.MainFormSettings.Record(this);
            Settings.Default.Save();
        }
        #endregion

        #region Start page related methods / properties
        public bool IsWebSiteReachable
        {
            get
            {
                try
                {
                    System.Uri uri = new System.Uri(Settings.Default.StartPageUrl);
                    System.Net.IPHostEntry objIPHE = System.Net.Dns.GetHostEntry(uri.DnsSafeHost);
                    return true;
                }
                catch (Exception ex)
                {
                    _log.Error(ex.ToString());
                    return false;
                }
            }
        }
        private void ShowStartPage(object sender)
        {
            if (!IsWebSiteReachable)
                return;
            _startPageCtrl.Url = new Uri(Properties.Settings.Default.StartPageUrl);
            _startPageCtrl.Visible = true;
            _branchViewCtrl.Visible = false;
            _pluginViewCtrl.Visible = false;
            _factoryViewCtrl.Visible = false;
            _webBrowser4PDF.Visible = false;
        }
        #endregion

        #region Helpers
        public void UpdateDocumentView()
        {
            if (_pluginViewCtrl.Visible)
                _pluginViewCtrl.Refresh();
        }
        #endregion

        #region Data members
        protected static readonly ILog _log = LogManager.GetLogger(typeof(MainForm));
        [NonSerialized]protected ProfileLoaderImpl _profileLoaderImpl;
        protected string _docName;
        #endregion


    }
}