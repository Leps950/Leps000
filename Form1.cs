using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.Xml;

namespace ConfigMaster
{
    public partial class Form1 : Form
    {
        private XmlDocument _currentXmlDoc;
        private string _currentFilePath;
        private bool _hasUnsavedChanges = false;
        private TreeNode _selectedNodeForContextMenu;

        private Stack<XmlAction> _undoStack = new Stack<XmlAction>();
        private Stack<XmlAction> _redoStack = new Stack<XmlAction>();
        private const int MAX_HISTORY = 50;

        private ToolStripMenuItem отменитьToolStripMenuItem;
        private ToolStripMenuItem повторитьToolStripMenuItem;

        public Form1()
        {
            InitializeComponent();
            InitializeMenu();
        }

        private void InitializeMenu()
        {
            отменитьToolStripMenuItem = new ToolStripMenuItem("Отменить");
            повторитьToolStripMenuItem = new ToolStripMenuItem("Повторить");

            отменитьToolStripMenuItem.Click += ОтменитьToolStripMenuItem_Click;
            повторитьToolStripMenuItem.Click += ПовторитьToolStripMenuItem_Click;

            var правкаToolStripMenuItem = menuStrip1.Items.Cast<ToolStripMenuItem>()
                .FirstOrDefault(item => item.Text == "Правка");

            if (правкаToolStripMenuItem != null)
            {
                правкаToolStripMenuItem.DropDownItems.Add(отменитьToolStripMenuItem);
                правкаToolStripMenuItem.DropDownItems.Add(повторитьToolStripMenuItem);
                правкаToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Text = "ConfigMaster - Редактор конфигураций";
            InitializeContextMenu();
            InitializeTemplatesMenu();
            propertyGrid1.PropertyValueChanged += PropertyGrid1_PropertyValueChanged;
            treeView1.MouseDown += TreeView1_MouseDown;

            this.KeyPreview = true;
            this.KeyDown += Form1_KeyDown;

            UpdateUndoRedoButtons();
        }

        private void InitializeContextMenu()
        {
            ContextMenuStrip contextMenu = new ContextMenuStrip();

            ToolStripMenuItem addNodeItem = new ToolStripMenuItem("Добавить узел");
            ToolStripMenuItem addAttributeItem = new ToolStripMenuItem("Добавить атрибут");
            ToolStripMenuItem deleteItem = new ToolStripMenuItem("Удалить");
            ToolStripMenuItem addFromTemplateItem = new ToolStripMenuItem("Добавить из шаблона");

            addNodeItem.Click += AddNodeItem_Click;
            addAttributeItem.Click += AddAttributeItem_Click;
            deleteItem.Click += DeleteItem_Click;
            addFromTemplateItem.Click += AddFromTemplateItem_Click;

            contextMenu.Items.Add(addNodeItem);
            contextMenu.Items.Add(addAttributeItem);
            contextMenu.Items.Add(addFromTemplateItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(deleteItem);

            treeView1.ContextMenuStrip = contextMenu;
        }

        private void InitializeTemplatesMenu()
        {
            var шаблоныToolStripMenuItem = new ToolStripMenuItem("Шаблоны");

            var управлениеШаблонамиToolStripMenuItem = new ToolStripMenuItem("Управление шаблонами");
            управлениеШаблонамиToolStripMenuItem.Click += (s, e) =>
            {
                using (var form = new TemplateManagerForm())
                {
                    form.ShowDialog();
                }
            };

            шаблоныToolStripMenuItem.DropDownItems.Add(управлениеШаблонамиToolStripMenuItem);
            menuStrip1.Items.Add(шаблоныToolStripMenuItem);

            // ДОБАВЛЯЕМ КРОН-РЕДАКТОР КАК ОТДЕЛЬНЫЙ ПУНКТ МЕНЮ РЯДОМ
            var cronEditorToolStripMenuItem = new ToolStripMenuItem("Редактор Cron");
            cronEditorToolStripMenuItem.Click += (s, e) =>
            {
                using (var form = new CronEditorForm())
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        MessageBox.Show($"Сгенерировано выражение: {form.CronExpression}", "Cron редактор");
                    }
                }
            };
            menuStrip1.Items.Add(cronEditorToolStripMenuItem); // Добавляем рядом в меню
        }

        private void AddFromTemplateItem_Click(object sender, EventArgs e)
        {
            if (_selectedNodeForContextMenu?.Tag is XmlNode parentNode)
            {
                using (var form = new TemplateManagerForm())
                {
                    if (form.ShowDialog() == DialogResult.OK && form.SelectedTemplate != null)
                    {
                        try
                        {
                            XmlDocument tempDoc = new XmlDocument();
                            tempDoc.LoadXml($"<root>{form.SelectedTemplate.XmlContent}</root>");

                            foreach (XmlNode childNode in tempDoc.DocumentElement.ChildNodes)
                            {
                                XmlNode importedNode = _currentXmlDoc.ImportNode(childNode, true);
                                parentNode.AppendChild(importedNode);

                                TreeNode newNode = new TreeNode(childNode.Name);
                                newNode.Tag = importedNode;
                                _selectedNodeForContextMenu.Nodes.Add(newNode);

                                if (childNode.Attributes != null)
                                {
                                    foreach (XmlAttribute attr in childNode.Attributes)
                                    {
                                        TreeNode attrNode = new TreeNode($"{attr.Name} = {attr.Value}");
                                        attrNode.Tag = attr;
                                        attrNode.ForeColor = Color.Blue;
                                        newNode.Nodes.Add(attrNode);
                                    }
                                }
                            }

                            _selectedNodeForContextMenu.Expand();
                            _hasUnsavedChanges = true;
                            UpdateWindowTitle();

                            AddToHistory(new XmlAddFromTemplateAction(parentNode, form.SelectedTemplate.XmlContent));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Ошибка при добавлении из шаблона: {ex.Message}", "Ошибка",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
        }

        private void TreeView1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                TreeNode node = treeView1.GetNodeAt(e.X, e.Y);
                if (node != null)
                {
                    treeView1.SelectedNode = node;
                    _selectedNodeForContextMenu = node;
                }
            }
        }

        private void ОткрытьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenConfigFile();
        }

        private void СохранитьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveConfigFile();
        }

        private void СохранитьКакToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveConfigFileAs();
        }

        private void ВыходToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void ОтменитьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Undo();
        }

        private void ПовторитьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Redo();
        }

        private void OpenConfigFile()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Файлы конфигурации|*.config;*.xml|Все файлы|*.*";
                openFileDialog.Title = "Открыть файл конфигурации";
                openFileDialog.Multiselect = false;
                openFileDialog.CheckFileExists = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string selectedFilePath = openFileDialog.FileName;
                        LoadConfigFile(selectedFilePath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при открытии файла:\n{ex.Message}",
                            "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void LoadConfigFile(string filePath)
        {
            try
            {
                _currentXmlDoc = new XmlDocument();
                _currentXmlDoc.Load(filePath);
                _currentFilePath = filePath;
                _hasUnsavedChanges = false;
                _undoStack.Clear();
                _redoStack.Clear();

                treeView1.Nodes.Clear();
                TreeNode rootNode = new TreeNode(Path.GetFileName(filePath));
                rootNode.Tag = _currentXmlDoc.DocumentElement;
                treeView1.Nodes.Add(rootNode);

                if (_currentXmlDoc.DocumentElement != null)
                {
                    AddXmlNodesToTree(_currentXmlDoc.DocumentElement, rootNode);
                }

                rootNode.Expand();
                UpdateWindowTitle();
                UpdateUndoRedoButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки XML:\n{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddXmlNodesToTree(XmlNode xmlNode, TreeNode treeNode)
        {
            foreach (XmlNode childNode in xmlNode.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.Element)
                {
                    string nodeText = childNode.Name;
                    if (childNode.Attributes != null && childNode.Attributes["name"] != null)
                    {
                        nodeText += $" ({childNode.Attributes["name"].Value})";
                    }

                    TreeNode childTreeNode = new TreeNode(nodeText);
                    childTreeNode.Tag = childNode;

                    if (childNode.Attributes != null)
                    {
                        foreach (XmlAttribute attr in childNode.Attributes)
                        {
                            TreeNode attrNode = new TreeNode($"{attr.Name} = {attr.Value}");
                            attrNode.Tag = attr;
                            attrNode.ForeColor = Color.Blue;
                            childTreeNode.Nodes.Add(attrNode);
                        }
                    }

                    if (childNode.HasChildNodes)
                    {
                        AddXmlNodesToTree(childNode, childTreeNode);
                    }

                    treeNode.Nodes.Add(childTreeNode);
                }
            }
        }

        private void SaveConfigFile()
        {
            try
            {
                if (_currentXmlDoc == null) return;
                if (string.IsNullOrEmpty(_currentFilePath))
                {
                    SaveConfigFileAs();
                    return;
                }

                _currentXmlDoc.Save(_currentFilePath);
                _hasUnsavedChanges = false;
                UpdateWindowTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла:\n{ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveConfigFileAs()
        {
            try
            {
                if (_currentXmlDoc == null) return;

                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "XML файлы|*.xml|Config файлы|*.config|Все файлы|*.*";
                    saveFileDialog.Title = "Сохранить конфигурацию как";
                    saveFileDialog.DefaultExt = ".config";

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        _currentFilePath = saveFileDialog.FileName;
                        _currentXmlDoc.Save(_currentFilePath);
                        _hasUnsavedChanges = false;
                        UpdateWindowTitle();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении файла:\n{ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateWindowTitle()
        {
            string title = "ConfigMaster - Редактор конфигураций";
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                title += " - " + Path.GetFileName(_currentFilePath);
                if (_hasUnsavedChanges) title += " *";
            }
            this.Text = title;
        }

        private void TreeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag is XmlNode xmlNode)
            {
                propertyGrid1.SelectedObject = new XmlNodeWrapper(xmlNode);
            }
            else if (e.Node.Tag is XmlAttribute xmlAttr)
            {
                propertyGrid1.SelectedObject = new XmlAttributeWrapper(xmlAttr);
            }
            else
            {
                propertyGrid1.SelectedObject = null;
            }
        }

        private void PropertyGrid1_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            _hasUnsavedChanges = true;
            UpdateWindowTitle();

            if (treeView1.SelectedNode != null)
            {
                if (treeView1.SelectedNode.Tag is XmlAttribute attr)
                {
                    treeView1.SelectedNode.Text = $"{attr.Name} = {attr.Value}";
                    AddToHistory(new XmlAttributeChangeAction(attr, e.OldValue as string, attr.Value));
                }
                else if (treeView1.SelectedNode.Tag is XmlNode node)
                {
                    string nodeText = node.Name;
                    if (node.Attributes != null && node.Attributes["name"] != null)
                    {
                        nodeText += $" ({node.Attributes["name"].Value})";
                    }
                    treeView1.SelectedNode.Text = nodeText;
                    AddToHistory(new XmlNodeValueChangeAction(node, e.OldValue as string, node.InnerText));
                }
            }
        }

        private void AddToHistory(XmlAction action)
        {
            _undoStack.Push(action);
            if (_undoStack.Count > MAX_HISTORY)
            {
                var list = _undoStack.ToList();
                list.RemoveAt(list.Count - 1);
                _undoStack = new Stack<XmlAction>(list.Reverse<XmlAction>());
            }
            _redoStack.Clear();
            UpdateUndoRedoButtons();
        }

        private void Undo()
        {
            if (_undoStack.Count > 0)
            {
                var action = _undoStack.Pop();
                action.Undo();
                _redoStack.Push(action);
                _hasUnsavedChanges = true;
                UpdateWindowTitle();
                UpdateUndoRedoButtons();
                RefreshTreeView();
            }
        }

        private void Redo()
        {
            if (_redoStack.Count > 0)
            {
                var action = _redoStack.Pop();
                action.Redo();
                _undoStack.Push(action);
                _hasUnsavedChanges = true;
                UpdateWindowTitle();
                UpdateUndoRedoButtons();
                RefreshTreeView();
            }
        }

        private void UpdateUndoRedoButtons()
        {
            отменитьToolStripMenuItem.Enabled = _undoStack.Count > 0;
            отменитьToolStripMenuItem.Text = _undoStack.Count > 0 ?
                $"Отменить ({_undoStack.Peek().Description})" : "Отменить";

            повторитьToolStripMenuItem.Enabled = _redoStack.Count > 0;
            повторитьToolStripMenuItem.Text = _redoStack.Count > 0 ?
                $"Повторить ({_redoStack.Peek().Description})" : "Повторить";
        }

        private void RefreshTreeView()
        {
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                LoadConfigFile(_currentFilePath);
            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.Z:
                        Undo();
                        e.Handled = true;
                        break;
                    case Keys.Y:
                        Redo();
                        e.Handled = true;
                        break;
                    case Keys.O:
                        OpenConfigFile();
                        e.Handled = true;
                        break;
                    case Keys.S:
                        if (e.Shift)
                            SaveConfigFileAs();
                        else
                            SaveConfigFile();
                        e.Handled = true;
                        break;
                }
            }
        }

        private void AddNodeItem_Click(object sender, EventArgs e)
        {
            if (_selectedNodeForContextMenu?.Tag is XmlNode parentNode)
            {
                using (var form = new AddNodeForm())
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            XmlElement newElement = _currentXmlDoc.CreateElement(form.NodeName);

                            if (!string.IsNullOrEmpty(form.NodeValue))
                            {
                                newElement.InnerText = form.NodeValue;
                            }

                            parentNode.AppendChild(newElement);

                            TreeNode newNode = new TreeNode(form.NodeName);
                            newNode.Tag = newElement;
                            _selectedNodeForContextMenu.Nodes.Add(newNode);
                            _selectedNodeForContextMenu.Expand();

                            _hasUnsavedChanges = true;
                            UpdateWindowTitle();

                            AddToHistory(new XmlAddNodeAction(parentNode, newElement));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
        }

        private void AddAttributeItem_Click(object sender, EventArgs e)
        {
            if (_selectedNodeForContextMenu?.Tag is XmlNode node)
            {
                using (var form = new AddAttributeForm())
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        try
                        {
                            XmlAttribute newAttr = _currentXmlDoc.CreateAttribute(form.AttributeName);
                            newAttr.Value = form.AttributeValue;
                            node.Attributes.Append(newAttr);

                            TreeNode attrNode = new TreeNode($"{newAttr.Name} = {newAttr.Value}");
                            attrNode.Tag = newAttr;
                            attrNode.ForeColor = Color.Blue;
                            _selectedNodeForContextMenu.Nodes.Add(attrNode);

                            _hasUnsavedChanges = true;
                            UpdateWindowTitle();

                            AddToHistory(new XmlAddAttributeAction(node, newAttr));
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
        }

        private void DeleteItem_Click(object sender, EventArgs e)
        {
            if (_selectedNodeForContextMenu == null) return;

            if (MessageBox.Show("Удалить выбранный элемент?", "Подтверждение",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    XmlAction action = null;

                    if (_selectedNodeForContextMenu.Tag is XmlNode xmlNode)
                    {
                        action = new XmlDeleteNodeAction(xmlNode.ParentNode, xmlNode);
                        xmlNode.ParentNode.RemoveChild(xmlNode);
                    }
                    else if (_selectedNodeForContextMenu.Tag is XmlAttribute xmlAttr)
                    {
                        action = new XmlDeleteAttributeAction(xmlAttr.OwnerElement, xmlAttr);
                        xmlAttr.OwnerElement.Attributes.Remove(xmlAttr);
                    }

                    _selectedNodeForContextMenu.Remove();
                    _hasUnsavedChanges = true;
                    UpdateWindowTitle();

                    if (action != null)
                    {
                        AddToHistory(action);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.CloseReason == CloseReason.UserClosing && _hasUnsavedChanges)
            {
                var result = MessageBox.Show("Есть несохраненные изменения. Сохранить?", "Подтверждение",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                    SaveConfigFile();
                else if (result == DialogResult.Cancel)
                    e.Cancel = true;
            }
        }

        public abstract class XmlAction
        {
            public abstract string Description { get; }
            public abstract void Undo();
            public abstract void Redo();
        }

        public class XmlAttributeChangeAction : XmlAction
        {
            private XmlAttribute _attribute;
            private string _oldValue;
            private string _newValue;

            public XmlAttributeChangeAction(XmlAttribute attribute, string oldValue, string newValue)
            {
                _attribute = attribute;
                _oldValue = oldValue;
                _newValue = newValue;
            }

            public override string Description => $"Изменение атрибута {_attribute.Name}";

            public override void Undo()
            {
                _attribute.Value = _oldValue;
            }

            public override void Redo()
            {
                _attribute.Value = _newValue;
            }
        }

        public class XmlNodeValueChangeAction : XmlAction
        {
            private XmlNode _node;
            private string _oldValue;
            private string _newValue;

            public XmlNodeValueChangeAction(XmlNode node, string oldValue, string newValue)
            {
                _node = node;
                _oldValue = oldValue;
                _newValue = newValue;
            }

            public override string Description => $"Изменение значения узла {_node.Name}";

            public override void Undo()
            {
                _node.InnerText = _oldValue;
            }

            public override void Redo()
            {
                _node.InnerText = _newValue;
            }
        }

        public class XmlAddFromTemplateAction : XmlAction
        {
            private XmlNode _parentNode;
            private string _xmlContent;
            private List<XmlNode> _addedNodes = new List<XmlNode>();

            public XmlAddFromTemplateAction(XmlNode parentNode, string xmlContent)
            {
                _parentNode = parentNode;
                _xmlContent = xmlContent;
            }

            public override string Description => "Добавление из шаблона";

            public override void Undo()
            {
                foreach (var node in _addedNodes)
                {
                    _parentNode.RemoveChild(node);
                }
                _addedNodes.Clear();
            }

            public override void Redo()
            {
                XmlDocument tempDoc = new XmlDocument();
                tempDoc.LoadXml($"<root>{_xmlContent}</root>");

                foreach (XmlNode childNode in tempDoc.DocumentElement.ChildNodes)
                {
                    XmlNode importedNode = _parentNode.OwnerDocument.ImportNode(childNode, true);
                    _parentNode.AppendChild(importedNode);
                    _addedNodes.Add(importedNode);
                }
            }
        }

        public class XmlAddNodeAction : XmlAction
        {
            private XmlNode _parentNode;
            private XmlNode _addedNode;

            public XmlAddNodeAction(XmlNode parentNode, XmlNode addedNode)
            {
                _parentNode = parentNode;
                _addedNode = addedNode;
            }

            public override string Description => $"Добавление узла {_addedNode.Name}";

            public override void Undo()
            {
                _parentNode.RemoveChild(_addedNode);
            }

            public override void Redo()
            {
                _parentNode.AppendChild(_addedNode);
            }
        }

        public class XmlAddAttributeAction : XmlAction
        {
            private XmlNode _ownerElement;
            private XmlAttribute _addedAttribute;

            public XmlAddAttributeAction(XmlNode ownerElement, XmlAttribute addedAttribute)
            {
                _ownerElement = ownerElement;
                _addedAttribute = addedAttribute;
            }

            public override string Description => $"Добавление атрибута {_addedAttribute.Name}";

            public override void Undo()
            {
                _ownerElement.Attributes.Remove(_addedAttribute);
            }

            public override void Redo()
            {
                _ownerElement.Attributes.Append(_addedAttribute);
            }
        }

        public class XmlDeleteNodeAction : XmlAction
        {
            private XmlNode _parentNode;
            private XmlNode _deletedNode;

            public XmlDeleteNodeAction(XmlNode parentNode, XmlNode deletedNode)
            {
                _parentNode = parentNode;
                _deletedNode = deletedNode;
            }

            public override string Description => $"Удаление узла {_deletedNode.Name}";

            public override void Undo()
            {
                _parentNode.AppendChild(_deletedNode);
            }

            public override void Redo()
            {
                _parentNode.RemoveChild(_deletedNode);
            }
        }

        public class XmlDeleteAttributeAction : XmlAction
        {
            private XmlElement _ownerElement;
            private XmlAttribute _deletedAttribute;

            public XmlDeleteAttributeAction(XmlElement ownerElement, XmlAttribute deletedAttribute)
            {
                _ownerElement = ownerElement;
                _deletedAttribute = deletedAttribute;
            }

            public override string Description => $"Удаление атрибута {_deletedAttribute.Name}";

            public override void Undo()
            {
                _ownerElement.Attributes.Append(_deletedAttribute);
            }

            public override void Redo()
            {
                _ownerElement.Attributes.Remove(_deletedAttribute);
            }
        }

        public class XmlNodeWrapper
        {
            private XmlNode _node;

            public XmlNodeWrapper(XmlNode node)
            {
                _node = node;
            }

            // Старые свойства остаются без изменений
            [Category("Основные")]
            [DisplayName("Имя узла")]
            [Description("Название XML элемента")]
            [ReadOnly(true)]
            public string Name => _node.Name;

            [Category("Информация")]
            [DisplayName("Тип узла")]
            [Description("Тип XML узла")]
            [ReadOnly(true)]
            public XmlNodeType NodeType => _node.NodeType;

            [Category("Информация")]
            [DisplayName("Дочерние узлы")]
            [Description("Количество дочерних элементов")]
            [ReadOnly(true)]
            public int ChildNodesCount => _node.ChildNodes.Count;

            // МЕНЯЕМ ЭТО СВОЙСТВО:
            [Category("Основные")]
            [DisplayName("Значение")]
            [Description("")] // ← МЕНЯЕМ НА ПУСТУЮ СТРОКУ
            [Editor(typeof(SmartValueEditor), typeof(UITypeEditor))]
            public string Value
            {
                get => _node.InnerText;
                set => _node.InnerText = value;
            }

            // ДОБАВЛЯЕМ НОВОЕ СВОЙСТВО ДЛЯ ОТОБРАЖЕНИЯ ЗНАЧЕНИЯ
            [Category(" ")] // Пустая категория, чтобы было в основном разделе
            [DisplayName("Текущее значение:")]
            [Description("{0}")] // ← Здесь будет отображаться значение
            [ReadOnly(true)]
            public string CurrentValue => string.IsNullOrEmpty(_node.InnerText) ? "(пусто)" : _node.InnerText;
        }

        public class XmlAttributeWrapper
        {
            private XmlAttribute _attribute;

            public XmlAttributeWrapper(XmlAttribute attribute)
            {
                _attribute = attribute;
            }

            // Старые свойства остаются без изменений
            [Category("Основные")]
            [DisplayName("Имя атрибута")]
            [Description("Название XML атрибута")]
            [ReadOnly(true)]
            public string Name => _attribute.Name;

            [Category("Информация")]
            [DisplayName("Родительский элемент")]
            [Description("Элемент, которому принадлежит атрибут")]
            [ReadOnly(true)]
            public string OwnerElement => _attribute.OwnerElement?.Name;

            // МЕНЯЕМ ЭТО СВОЙСТВО:
            [Category("Основные")]
            [DisplayName("Значение")]
            [Description("")] // ← МЕНЯЕМ НА ПУСТУЮ СТРОКУ
            [Editor(typeof(SmartValueEditor), typeof(UITypeEditor))]
            public string Value
            {
                get => _attribute.Value;
                set => _attribute.Value = value;
            }

            // ДОБАВЛЯЕМ НОВОЕ СВОЙСТВО ДЛЯ ОТОБРАЖЕНИЯ ЗНАЧЕНИЯ
            [Category(" ")] // Пустая категория
            [DisplayName("Текущее значение:")]
            [Description("{0}")] // ← Здесь будет отображаться значение
            [ReadOnly(true)]
            public string CurrentValue => string.IsNullOrEmpty(_attribute.Value) ? "(пусто)" : _attribute.Value;
        }

        public class SmartValueEditor : UITypeEditor
        {
            private IWindowsFormsEditorService _editorService;

            public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
            {
                if (context?.PropertyDescriptor == null)
                    return UITypeEditorEditStyle.None;

                string currentValue = context.PropertyDescriptor.GetValue(context.Instance) as string;

                if (IsBooleanValue(currentValue))
                    return UITypeEditorEditStyle.DropDown;

                return UITypeEditorEditStyle.Modal;
            }

            public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
            {
                _editorService = provider?.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
                if (_editorService == null)
                    return value;

                string currentValue = value as string;

                if (IsBooleanValue(currentValue))
                {
                    ListBox listBox = new ListBox();
                    listBox.Items.AddRange(new object[] { "True", "False" });
                    listBox.SelectedItem = currentValue;
                    listBox.Click += (s, e) => _editorService.CloseDropDown();

                    _editorService.DropDownControl(listBox);
                    return listBox.SelectedItem ?? value;
                }
                else
                {
                    using (TextEditorForm editorForm = new TextEditorForm())
                    {
                        editorForm.Value = currentValue;
                        if (editorForm.ShowDialog() == DialogResult.OK)
                        {
                            return editorForm.Value;
                        }
                    }
                }

                return value;
            }

            private bool IsBooleanValue(string value)
            {
                if (string.IsNullOrEmpty(value)) return false;
                return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                       value.Equals("false", StringComparison.OrdinalIgnoreCase);
            }
        }

        public class TextEditorForm : Form
        {
            public string Value { get; set; }

            private TextBox txtValue;
            private Button btnOk;
            private Button btnCancel;

            public TextEditorForm()
            {
                InitializeComponent();
            }

            private void InitializeComponent()
            {
                this.Text = "Редактирование значения";
                this.Size = new Size(500, 300);
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.StartPosition = FormStartPosition.CenterParent;
                this.MaximizeBox = false;
                this.MinimizeBox = false;

                Label lblValue = new Label { Text = "Значение:", Location = new Point(10, 10), Width = 80 };

                txtValue = new TextBox
                {
                    Location = new Point(100, 10),
                    Width = 370,
                    Height = 200,
                    Multiline = true,
                    ScrollBars = ScrollBars.Both,
                    Text = Value
                };

                btnOk = new Button { Text = "OK", Location = new Point(150, 220), Width = 75 };
                btnCancel = new Button { Text = "Отмена", Location = new Point(240, 220), Width = 75 };

                btnOk.Click += (s, e) =>
                {
                    Value = txtValue.Text;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                };

                btnCancel.Click += (s, e) =>
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                };

                this.AcceptButton = btnOk;
                this.CancelButton = btnCancel;

                this.Controls.AddRange(new Control[] {
                    lblValue, txtValue, btnOk, btnCancel
                });

                if (!string.IsNullOrEmpty(Value))
                {
                    txtValue.Text = Value;
                }
            }
        }

        public class AddNodeForm : Form
        {
            public string NodeName { get; private set; }
            public string NodeValue { get; private set; }

            private TextBox txtName;
            private TextBox txtValue;
            private Button btnOk;
            private Button btnCancel;
            private Button btnValueEditor;

            public AddNodeForm()
            {
                InitializeComponent();
            }

            private void InitializeComponent()
            {
                this.Text = "Добавить узел";
                this.Size = new Size(500, 200);
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.StartPosition = FormStartPosition.CenterParent;
                this.MaximizeBox = false;
                this.MinimizeBox = false;

                Label lblName = new Label { Text = "Имя узла:", Location = new Point(10, 10), Width = 80 };
                txtName = new TextBox { Location = new Point(100, 10), Width = 150 };

                Label lblValue = new Label { Text = "Значение:", Location = new Point(10, 40), Width = 80 };

                txtValue = new TextBox
                {
                    Location = new Point(100, 40),
                    Width = 250,
                    Height = 60,
                    Multiline = true
                };

                btnValueEditor = new Button { Text = "...", Location = new Point(360, 40), Width = 30, Height = 60 };
                btnValueEditor.Click += (s, e) =>
                {
                    using (TextEditorForm editor = new TextEditorForm())
                    {
                        editor.Value = txtValue.Text;
                        if (editor.ShowDialog() == DialogResult.OK)
                        {
                            txtValue.Text = editor.Value;
                        }
                    }
                };

                btnOk = new Button { Text = "OK", Location = new Point(150, 110), Width = 75 };
                btnCancel = new Button { Text = "Отмена", Location = new Point(240, 110), Width = 75 };

                btnOk.Click += (s, e) =>
                {
                    if (string.IsNullOrEmpty(txtName.Text))
                    {
                        MessageBox.Show("Введите имя узла", "Ошибка",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    NodeName = txtName.Text;
                    NodeValue = txtValue.Text;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                };

                btnCancel.Click += (s, e) =>
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                };

                this.AcceptButton = btnOk;
                this.CancelButton = btnCancel;

                this.Controls.AddRange(new Control[] {
                    lblName, txtName, lblValue, txtValue, btnValueEditor, btnOk, btnCancel
                });
            }
        }

        public class AddAttributeForm : Form
        {
            public string AttributeName { get; private set; }
            public string AttributeValue { get; private set; }

            private TextBox txtName;
            private TextBox txtValue;
            private Button btnOk;
            private Button btnCancel;
            private Button btnValueEditor;

            public AddAttributeForm()
            {
                InitializeComponent();
            }

            private void InitializeComponent()
            {
                this.Text = "Добавить атрибут";
                this.Size = new Size(500, 200);
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.StartPosition = FormStartPosition.CenterParent;
                this.MaximizeBox = false;
                this.MinimizeBox = false;

                Label lblName = new Label { Text = "Имя атрибута:", Location = new Point(10, 10), Width = 80 };
                txtName = new TextBox { Location = new Point(100, 10), Width = 150 };

                Label lblValue = new Label { Text = "Значение:", Location = new Point(10, 40), Width = 80 };

                txtValue = new TextBox
                {
                    Location = new Point(100, 40),
                    Width = 250,
                    Height = 60,
                    Multiline = true
                };

                btnValueEditor = new Button { Text = "...", Location = new Point(360, 40), Width = 30, Height = 60 };
                btnValueEditor.Click += (s, e) =>
                {
                    using (TextEditorForm editor = new TextEditorForm())
                    {
                        editor.Value = txtValue.Text;
                        if (editor.ShowDialog() == DialogResult.OK)
                        {
                            txtValue.Text = editor.Value;
                        }
                    }
                };

                btnOk = new Button { Text = "OK", Location = new Point(150, 110), Width = 75 };
                btnCancel = new Button { Text = "Отмена", Location = new Point(240, 110), Width = 75 };

                btnOk.Click += (s, e) =>
                {
                    if (string.IsNullOrEmpty(txtName.Text))
                    {
                        MessageBox.Show("Введите имя атрибута", "Ошибка",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    AttributeName = txtName.Text;
                    AttributeValue = txtValue.Text;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                };

                btnCancel.Click += (s, e) =>
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                };

                this.AcceptButton = btnOk;
                this.CancelButton = btnCancel;

                this.Controls.AddRange(new Control[] {
                    lblName, txtName, lblValue, txtValue, btnValueEditor, btnOk, btnCancel
                });
            }
        }

        public static class TemplateManager
        {
            private static List<XmlTemplate> _templates = new List<XmlTemplate>();
            private static string TemplatesFilePath = Path.Combine(Application.StartupPath, "templates.xml");

            static TemplateManager()
            {
                LoadTemplates();
            }

            public static void AddTemplate(XmlTemplate template)
            {
                _templates.Add(template);
                SaveTemplates();
            }

            public static void RemoveTemplate(string name)
            {
                var template = _templates.FirstOrDefault(t => t.Name == name);
                if (template != null)
                {
                    _templates.Remove(template);
                    SaveTemplates();
                }
            }

            public static List<XmlTemplate> GetTemplates()
            {
                return new List<XmlTemplate>(_templates);
            }

            public static XmlTemplate GetTemplate(string name)
            {
                return _templates.FirstOrDefault(t => t.Name == name);
            }

            private static void LoadTemplates()
            {
                if (File.Exists(TemplatesFilePath))
                {
                    try
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.Load(TemplatesFilePath);

                        _templates.Clear();
                        foreach (XmlNode node in doc.SelectNodes("/templates/template"))
                        {
                            var template = new XmlTemplate
                            {
                                Name = node.Attributes["name"]?.Value,
                                XmlContent = node.InnerXml
                            };
                            _templates.Add(template);
                        }
                    }
                    catch
                    {
                        _templates = new List<XmlTemplate>();
                    }
                }
            }

            private static void SaveTemplates()
            {
                try
                {
                    XmlDocument doc = new XmlDocument();
                    XmlElement root = doc.CreateElement("templates");
                    doc.AppendChild(root);

                    foreach (var template in _templates)
                    {
                        XmlElement templateElement = doc.CreateElement("template");
                        templateElement.SetAttribute("name", template.Name);
                        templateElement.InnerXml = template.XmlContent;
                        root.AppendChild(templateElement);
                    }

                    doc.Save(TemplatesFilePath);
                }
                catch
                {
                }
            }
        }

        public class XmlTemplate
        {
            public string Name { get; set; }
            public string XmlContent { get; set; }
        }

        public class TemplateManagerForm : Form
        {
            private ListBox lstTemplates;
            private Button btnAdd;
            private Button btnEdit;
            private Button btnDelete;
            private Button btnUse;

            public XmlTemplate SelectedTemplate { get; private set; }

            public TemplateManagerForm()
            {
                InitializeComponent();
                LoadTemplates();
            }

            private void InitializeComponent()
            {
                this.Text = "Управление шаблонами";
                this.Size = new Size(400, 300);
                this.StartPosition = FormStartPosition.CenterParent;
                this.FormBorderStyle = FormBorderStyle.FixedDialog;

                lstTemplates = new ListBox { Location = new Point(10, 10), Size = new Size(280, 200) };
                lstTemplates.SelectedIndexChanged += (s, e) => UpdateButtons();

                btnAdd = new Button { Text = "Добавить", Location = new Point(300, 10), Width = 80 };
                btnEdit = new Button { Text = "Редактировать", Location = new Point(300, 40), Width = 80 };
                btnDelete = new Button { Text = "Удалить", Location = new Point(300, 70), Width = 80 };
                btnUse = new Button { Text = "Использовать", Location = new Point(300, 100), Width = 80 };

                btnAdd.Click += (s, e) => AddTemplate();
                btnEdit.Click += (s, e) => EditTemplate();
                btnDelete.Click += (s, e) => DeleteTemplate();
                btnUse.Click += (s, e) => UseTemplate();

                Button btnCancel = new Button { Text = "Отмена", Location = new Point(300, 130), Width = 80 };
                btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

                this.Controls.AddRange(new Control[] {
                    lstTemplates, btnAdd, btnEdit, btnDelete, btnUse, btnCancel
                });

                UpdateButtons();
            }

            private void LoadTemplates()
            {
                lstTemplates.Items.Clear();
                foreach (var template in TemplateManager.GetTemplates())
                {
                    lstTemplates.Items.Add(template.Name);
                }
            }

            private void UpdateButtons()
            {
                bool hasSelection = lstTemplates.SelectedIndex >= 0;
                btnEdit.Enabled = hasSelection;
                btnDelete.Enabled = hasSelection;
                btnUse.Enabled = hasSelection;
            }

            private void AddTemplate()
            {
                using (var form = new TemplateEditForm())
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        TemplateManager.AddTemplate(form.Template);
                        LoadTemplates();
                    }
                }
            }

            private void EditTemplate()
            {
                if (lstTemplates.SelectedItem != null)
                {
                    var templateName = lstTemplates.SelectedItem.ToString();
                    var template = TemplateManager.GetTemplate(templateName);

                    using (var form = new TemplateEditForm(template))
                    {
                        if (form.ShowDialog() == DialogResult.OK)
                        {
                            TemplateManager.RemoveTemplate(templateName);
                            TemplateManager.AddTemplate(form.Template);
                            LoadTemplates();
                        }
                    }
                }
            }

            private void DeleteTemplate()
            {
                if (lstTemplates.SelectedItem != null)
                {
                    var templateName = lstTemplates.SelectedItem.ToString();
                    if (MessageBox.Show($"Удалить шаблон '{templateName}'?", "Подтверждение",
                        MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        TemplateManager.RemoveTemplate(templateName);
                        LoadTemplates();
                    }
                }
            }

            private void UseTemplate()
            {
                if (lstTemplates.SelectedItem != null)
                {
                    var templateName = lstTemplates.SelectedItem.ToString();
                    SelectedTemplate = TemplateManager.GetTemplate(templateName);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
        }

        public class TemplateEditForm : Form
        {
            public XmlTemplate Template { get; private set; }

            private TextBox txtName;
            private TextBox txtXml;

            public TemplateEditForm() : this(null) { }

            public TemplateEditForm(XmlTemplate existingTemplate)
            {
                Template = existingTemplate ?? new XmlTemplate();
                InitializeComponent();
            }

            private void InitializeComponent()
            {
                this.Text = Template.Name == null ? "Новый шаблон" : "Редактирование шаблона";
                this.Size = new Size(500, 400);
                this.StartPosition = FormStartPosition.CenterParent;

                Label lblName = new Label { Text = "Название:", Location = new Point(10, 10), Width = 80 };
                txtName = new TextBox { Location = new Point(100, 10), Width = 200, Text = Template.Name ?? "" };

                Label lblXml = new Label { Text = "XML содержимое:", Location = new Point(10, 40), Width = 100 };
                txtXml = new TextBox
                {
                    Location = new Point(10, 70),
                    Size = new Size(460, 250),
                    Multiline = true,
                    ScrollBars = ScrollBars.Both,
                    Text = Template.XmlContent ?? ""
                };

                Button btnOk = new Button { Text = "OK", Location = new Point(100, 330), Width = 80 };
                Button btnCancel = new Button { Text = "Отмена", Location = new Point(190, 330), Width = 80 };

                btnOk.Click += (s, e) =>
                {
                    if (string.IsNullOrEmpty(txtName.Text))
                    {
                        MessageBox.Show("Введите название шаблона");
                        return;
                    }
                    Template.Name = txtName.Text;
                    Template.XmlContent = txtXml.Text;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                };

                btnCancel.Click += (s, e) =>
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                };

                this.Controls.AddRange(new Control[] {
                    lblName, txtName, lblXml, txtXml, btnOk, btnCancel
                });
            }
        }
    }
}