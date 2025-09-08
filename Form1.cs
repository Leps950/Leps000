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

namespace LogusConfigEditor
{
    public partial class Form1 : Form
    {
        private XmlDocument _currentXmlDoc;
        private string _currentFilePath;
        private bool _hasUnsavedChanges = false;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Text = "Редактор конфигурации Logus";
            InitializeContextMenu();
            propertyGrid1.PropertyValueChanged += propertyGrid1_PropertyValueChanged;
        }

        private void файлToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Обработчик клика на меню "Файл"
        }

        private void открытьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenConfigFile();
        }

        private void сохранитьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveConfigFile();
        }

        private void сохранитьКакToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveConfigFileAs();
        }

        private void выходToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Вы уверены, что хотите выйти?", "Подтверждение выхода",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Application.Exit();
            }
        }

        private void OpenConfigFile()
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Файлы конфигурации|*.config;*.xml|Все файлы|*.*";
                openFileDialog.Title = "Открыть файл конфигурации Logus";
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
                UpdateWindowTitle();

                // Очищаем дерево и строим заново
                treeView1.Nodes.Clear();
                TreeNode rootNode = new TreeNode("Конфигурация");
                treeView1.Nodes.Add(rootNode);

                // Добавляем узлы XML в дерево
                if (_currentXmlDoc.DocumentElement != null)
                {
                    AddXmlNodesToTree(_currentXmlDoc.DocumentElement, rootNode);
                }

                rootNode.Expand();

                // Просто обновляем заголовок, без сообщений
                UpdateWindowTitle();
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
                    TreeNode childTreeNode = new TreeNode(childNode.Name);
                    childTreeNode.Tag = childNode;

                    // Добавляем атрибуты как дочерние узлы
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

                    // Рекурсивно добавляем дочерние элементы
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
                if (_currentXmlDoc == null)
                {
                    MessageBox.Show("Нет загруженного файла для сохранения.", "Информация",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (string.IsNullOrEmpty(_currentFilePath))
                {
                    SaveConfigFileAs();
                    return;
                }

                _currentXmlDoc.Save(_currentFilePath);
                _hasUnsavedChanges = false;
                UpdateWindowTitle();

                // Короткое сообщение о успешном сохранении
                MessageBox.Show("Файл сохранен", "Успех",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                if (_currentXmlDoc == null)
                {
                    MessageBox.Show("Нет загруженного файла для сохранения.", "Информация",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.Filter = "XML файлы|*.xml|Config файлы|*.config|Все файлы|*.*";
                    saveFileDialog.Title = "Сохранить конфигурацию как";
                    saveFileDialog.DefaultExt = ".config";
                    saveFileDialog.AddExtension = true;

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        _currentFilePath = saveFileDialog.FileName;
                        _currentXmlDoc.Save(_currentFilePath);
                        _hasUnsavedChanges = false;

                        // Короткое сообщение о успешном сохранении
                        MessageBox.Show("Файл сохранен", "Успех",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);

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
            string title = "Редактор конфигурации Logus";
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                title += " - " + Path.GetFileName(_currentFilePath);
                if (_hasUnsavedChanges) title += " *";
            }
            this.Text = title;
        }

        // Обработчик для выбора узла в дереве - БЕЗ MessageBox!
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag is XmlNode xmlNode)
            {
                // Отображаем свойства узла в PropertyGrid
                propertyGrid1.SelectedObject = new XmlNodeWrapper(xmlNode);
            }
            else if (e.Node.Tag is XmlAttribute xmlAttr)
            {
                // Отображаем свойства атрибута в PropertyGrid
                propertyGrid1.SelectedObject = new XmlAttributeWrapper(xmlAttr);
            }
            else
            {
                propertyGrid1.SelectedObject = null;
            }
        }

        private void propertyGrid1_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            // Помечаем, что есть несохраненные изменения
            _hasUnsavedChanges = true;
            UpdateWindowTitle();

            // Обновляем отображение в дереве БЕЗ перезагрузки файла
            if (treeView1.SelectedNode != null)
            {
                if (treeView1.SelectedNode.Tag is XmlAttribute attr)
                {
                    // Обновляем текст узла атрибута
                    treeView1.SelectedNode.Text = $"{attr.Name} = {attr.Value}";
                }
                else if (treeView1.SelectedNode.Tag is XmlNode node)
                {
                    // Для узлов можно обновить отображение если нужно
                    treeView1.SelectedNode.Text = node.Name;
                }
            }
        }

        // Эти методы остаются, но не используются для обновления дерева
        private void RefreshTreeView()
        {
            // Этот метод теперь не вызывается автоматически
        }

        private void SaveExpandedNodes(TreeNodeCollection nodes, List<string> expandedNodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.IsExpanded)
                {
                    expandedNodes.Add(node.FullPath);
                }
                SaveExpandedNodes(node.Nodes, expandedNodes);
            }
        }

        private void RestoreExpandedNodes(TreeNodeCollection nodes, List<string> expandedNodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (expandedNodes.Contains(node.FullPath))
                {
                    node.Expand();
                }
                RestoreExpandedNodes(node.Nodes, expandedNodes);
            }
        }

        private void InitializeContextMenu()
        {
            ContextMenuStrip contextMenu = new ContextMenuStrip();

            ToolStripMenuItem addNodeItem = new ToolStripMenuItem("Добавить узел");
            ToolStripMenuItem addAttributeItem = new ToolStripMenuItem("Добавить атрибут");
            ToolStripMenuItem deleteItem = new ToolStripMenuItem("Удалить");

            addNodeItem.Click += AddNodeItem_Click;
            addAttributeItem.Click += AddAttributeItem_Click;
            deleteItem.Click += DeleteItem_Click;

            contextMenu.Items.Add(addNodeItem);
            contextMenu.Items.Add(addAttributeItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(deleteItem);

            treeView1.ContextMenuStrip = contextMenu;
        }

        private void AddNodeItem_Click(object sender, EventArgs e)
        {
            // Логика добавления нового узла
            MessageBox.Show("Функция добавления узла будет реализована");
        }

        private void AddAttributeItem_Click(object sender, EventArgs e)
        {
            // Логика добавления нового атрибута
            MessageBox.Show("Функция добавления атрибута будет реализована");
        }

        private void DeleteItem_Click(object sender, EventArgs e)
        {
            // Логика удаления элемента
            MessageBox.Show("Функция удаления будет реализована");
        }

        // Кастомный конвертер для булевых значений с выпадающим списком
        public class BooleanConverter : TypeConverter
        {
            public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
            {
                return true;
            }

            public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
            {
                return true;
            }

            public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
            {
                return new StandardValuesCollection(new[] { true, false });
            }

            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
            }

            public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
            {
                if (value is string stringValue)
                {
                    if (bool.TryParse(stringValue, out bool result))
                        return result;
                }
                return base.ConvertFrom(context, culture, value);
            }
        }

        // Дополнительный класс для удобного редактирования булевых значений
        public class BooleanEditor : UITypeEditor
        {
            private IWindowsFormsEditorService _editorService;

            public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
            {
                return UITypeEditorEditStyle.DropDown;
            }

            public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
            {
                _editorService = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));

                ListBox listBox = new ListBox();
                listBox.Items.Add("true");
                listBox.Items.Add("false");
                listBox.SelectedItem = value?.ToString()?.ToLower();
                listBox.Click += (s, e) => _editorService.CloseDropDown();

                _editorService.DropDownControl(listBox);

                return listBox.SelectedItem != null ? bool.Parse(listBox.SelectedItem.ToString()) : value;
            }
        }

        // Редактор для cron-выражений
        public class CronEditor : UITypeEditor
        {
            private IWindowsFormsEditorService _editorService;

            public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
            {
                return UITypeEditorEditStyle.Modal;
            }

            public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
            {
                _editorService = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));

                if (_editorService != null)
                {
                    using (CronEditorForm editorForm = new CronEditorForm())
                    {
                        editorForm.CronExpression = value as string;
                        if (editorForm.ShowDialog() == DialogResult.OK)
                        {
                            return editorForm.CronExpression;
                        }
                    }
                }

                return value;
            }
        }

        // Класс-обертка для XmlNode
        public class XmlNodeWrapper
        {
            private XmlNode _node;

            public XmlNodeWrapper(XmlNode node)
            {
                _node = node;
            }

            [Category("Основные")]
            [DisplayName("Имя узла")]
            [Description("Название XML элемента")]
            [ReadOnly(true)]
            public string Name => _node.Name;

            [Category("Основные")]
            [DisplayName("Значение")]
            [Description("Текстовое содержимое узла")]
            public string Value
            {
                get => _node.InnerText;
                set => _node.InnerText = value;
            }

            // Автоматическое определение булевых значений
            [Category("Основные")]
            [DisplayName("Логическое значение")]
            [Description("Значение true/false (автоматическое определение)")]
            [Editor(typeof(BooleanEditor), typeof(UITypeEditor))]
            [TypeConverter(typeof(BooleanConverter))]
            public bool? BooleanValue
            {
                get
                {
                    if (bool.TryParse(_node.InnerText, out bool result))
                        return result;
                    return null;
                }
                set
                {
                    if (value.HasValue)
                        _node.InnerText = value.Value.ToString().ToLower();
                }
            }

            // Свойство для cron-выражений
            [Category("Расписание")]
            [DisplayName("Cron выражение")]
            [Description("Расписание в формате Cron")]
            [Editor(typeof(CronEditor), typeof(UITypeEditor))]
            public string CronSchedule
            {
                get => _node.InnerText;
                set => _node.InnerText = value;
            }

            [Browsable(false)]
            public bool ShouldSerializeBooleanValue()
            {
                return bool.TryParse(_node.InnerText, out _);
            }

            [Browsable(false)]
            public bool ShouldSerializeCronSchedule()
            {
                // Показывать свойство только если это похоже на cron
                return _node.InnerText.Contains('*') || _node.InnerText.Contains('/') ||
                       _node.InnerText.Contains('-') || _node.InnerText.Contains(',');
            }

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
        }

        // Класс-обертка для XmlAttribute
        public class XmlAttributeWrapper
        {
            private XmlAttribute _attribute;

            public XmlAttributeWrapper(XmlAttribute attribute)
            {
                _attribute = attribute;
            }

            [Category("Основные")]
            [DisplayName("Имя атрибута")]
            [Description("Название XML атрибута")]
            [ReadOnly(true)]
            public string Name => _attribute.Name;

            [Category("Основные")]
            [DisplayName("Значение")]
            [Description("Значение атрибута")]
            public string Value
            {
                get => _attribute.Value;
                set => _attribute.Value = value;
            }

            // Автоматическое определение булевых значений для атрибутов
            [Category("Основные")]
            [DisplayName("Логическое значение")]
            [Description("Значение true/false (автоматическое определение)")]
            [Editor(typeof(BooleanEditor), typeof(UITypeEditor))]
            [TypeConverter(typeof(BooleanConverter))]
            public bool? BooleanValue
            {
                get
                {
                    if (bool.TryParse(_attribute.Value, out bool result))
                        return result;
                    return null;
                }
                set
                {
                    if (value.HasValue)
                        _attribute.Value = value.Value.ToString().ToLower();
                }
            }

            // Свойство для cron-выражений в атрибутах
            [Category("Расписание")]
            [DisplayName("Cron выражение")]
            [Description("Расписание в формате Cron")]
            [Editor(typeof(CronEditor), typeof(UITypeEditor))]
            public string CronSchedule
            {
                get => _attribute.Value;
                set => _attribute.Value = value;
            }

            [Browsable(false)]
            public bool ShouldSerializeBooleanValue()
            {
                return bool.TryParse(_attribute.Value, out _);
            }

            [Browsable(false)]
            public bool ShouldSerializeCronSchedule()
            {
                // Показывать свойство только если это похоже на cron
                return _attribute.Value.Contains('*') || _attribute.Value.Contains('/') ||
                       _attribute.Value.Contains('-') || _attribute.Value.Contains(',');
            }

            [Category("Информация")]
            [DisplayName("Родительский элемент")]
            [Description("Элемент, которому принадлежит атрибут")]
            [ReadOnly(true)]
            public string OwnerElement => _attribute.OwnerElement?.Name;
        }
    }

    // Форма для редактирования cron-выражений с пояснением
    public class CronEditorForm : Form
    {
        public string CronExpression { get; set; }

        private ComboBox cmbMinutes;
        private ComboBox cmbHours;
        private ComboBox cmbDays;
        private ComboBox cmbMonths;
        private ComboBox cmbWeekDays;
        private Button btnOk;
        private Button btnCancel;
        private Label lblCronResult;
        private TextBox txtExplanation;
        private ComboBox cmbPresets;

        public CronEditorForm()
        {
            InitializeComponent();
            LoadPresets();
        }

        private void InitializeComponent()
        {
            this.Text = "Редактор расписания Cron";
            this.Size = new Size(500, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9);

            // Presets
            Label lblPresets = new Label { Text = "Шаблоны:", Location = new Point(10, 10), Width = 80 };
            cmbPresets = new ComboBox { Location = new Point(100, 10), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbPresets.SelectedIndexChanged += PresetSelected;

            // Поля для редактирования
            Label lblMin = new Label { Text = "Минуты:", Location = new Point(10, 40), Width = 80 };
            cmbMinutes = new ComboBox { Location = new Point(100, 40), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };

            Label lblHours = new Label { Text = "Часы:", Location = new Point(10, 70), Width = 80 };
            cmbHours = new ComboBox { Location = new Point(100, 70), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };

            Label lblDays = new Label { Text = "Дни месяца:", Location = new Point(10, 100), Width = 80 };
            cmbDays = new ComboBox { Location = new Point(100, 100), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };

            Label lblMonths = new Label { Text = "Месяцы:", Location = new Point(10, 130), Width = 80 };
            cmbMonths = new ComboBox { Location = new Point(100, 130), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };

            Label lblWeekDays = new Label { Text = "Дни недели:", Location = new Point(10, 160), Width = 80 };
            cmbWeekDays = new ComboBox { Location = new Point(100, 160), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };

            // Результат cron
            lblCronResult = new Label { Text = "Cron выражение: ", Location = new Point(10, 190), Width = 460 };
            lblCronResult.Font = new Font(lblCronResult.Font, FontStyle.Bold);

            // Пояснение
            Label lblExplain = new Label { Text = "Пояснение:", Location = new Point(10, 210), Width = 80 };
            txtExplanation = new TextBox
            {
                Location = new Point(10, 230),
                Width = 460,
                Height = 100,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.LightYellow
            };

            // Кнопки
            btnOk = new Button { Text = "OK", Location = new Point(100, 340), Width = 80 };
            btnCancel = new Button { Text = "Отмена", Location = new Point(190, 340), Width = 80 };

            btnOk.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            // События для обновления
            cmbMinutes.SelectedIndexChanged += UpdateCronExpression;
            cmbHours.SelectedIndexChanged += UpdateCronExpression;
            cmbDays.SelectedIndexChanged += UpdateCronExpression;
            cmbMonths.SelectedIndexChanged += UpdateCronExpression;
            cmbWeekDays.SelectedIndexChanged += UpdateCronExpression;

            this.Controls.AddRange(new Control[] {
            lblPresets, cmbPresets,
            lblMin, cmbMinutes,
            lblHours, cmbHours,
            lblDays, cmbDays,
            lblMonths, cmbMonths,
            lblWeekDays, cmbWeekDays,
            lblCronResult, lblExplain, txtExplanation,
            btnOk, btnCancel
        });
        }

        private void LoadPresets()
        {
            // Шаблоны
            cmbPresets.Items.AddRange(new object[] {
            "Выберите шаблон...",
            "Каждую минуту",
            "Каждый час",
            "Ежедневно в полночь",
            "Ежедневно в 6:00",
            "Ежедневно в 12:00",
            "Ежедневно в 18:00",
            "По рабочим дням в 9:00",
            "По выходным в 10:00",
            "Еженедельно в воскресенье",
            "Ежемесячно 1 числа",
            "Каждые 5 минут",
            "Каждые 30 минут",
            "Каждые 2 часа"
        });
            cmbPresets.SelectedIndex = 0;

            // Минуты
            cmbMinutes.Items.AddRange(new object[] { "*", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10",
            "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "30",
            "31", "32", "33", "34", "35", "36", "37", "38", "39", "40", "41", "42", "43", "44", "45", "46", "47", "48", "49", "50",
            "51", "52", "53", "54", "55", "56", "57", "58", "59", "*/5", "*/10", "*/15", "*/30" });

            // Часы
            cmbHours.Items.AddRange(new object[] { "*", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11",
            "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "*/2", "*/3", "*/4", "*/6", "*/8", "*/12" });

            // Дни месяца
            cmbDays.Items.AddRange(new object[] { "*", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10",
            "11", "12", "13", "14", "15", "16", "17", "18", "19", "20",
            "21", "22", "23", "24", "25", "26", "27", "28", "29", "30", "31" });

            // Месяцы
            cmbMonths.Items.AddRange(new object[] { "*", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12" });

            // Дни недели (0-6, где 0=воскресенье, 1=понедельник)
            cmbWeekDays.Items.AddRange(new object[] { "*", "0", "1", "2", "3", "4", "5", "6", "1-5", "0,6" });

            // Установка значений по умолчанию
            cmbMinutes.SelectedItem = "*";
            cmbHours.SelectedItem = "*";
            cmbDays.SelectedItem = "*";
            cmbMonths.SelectedItem = "*";
            cmbWeekDays.SelectedItem = "*";
        }

        private void PresetSelected(object sender, EventArgs e)
        {
            switch (cmbPresets.SelectedIndex)
            {
                case 1: // Каждую минуту
                    SetCronValues("*", "*", "*", "*", "*");
                    break;
                case 2: // Каждый час
                    SetCronValues("0", "*", "*", "*", "*");
                    break;
                case 3: // Ежедневно в полночь
                    SetCronValues("0", "0", "*", "*", "*");
                    break;
                case 4: // Ежедневно в 6:00
                    SetCronValues("0", "6", "*", "*", "*");
                    break;
                case 5: // Ежедневно в 12:00
                    SetCronValues("0", "12", "*", "*", "*");
                    break;
                case 6: // Ежедневно в 18:00
                    SetCronValues("0", "18", "*", "*", "*");
                    break;
                case 7: // По рабочим дням в 9:00
                    SetCronValues("0", "9", "*", "*", "1-5");
                    break;
                case 8: // По выходным в 10:00
                    SetCronValues("0", "10", "*", "*", "0,6");
                    break;
                case 9: // Еженедельно в воскресенье
                    SetCronValues("0", "0", "*", "*", "0");
                    break;
                case 10: // Ежемесячно 1 числа
                    SetCronValues("0", "0", "1", "*", "*");
                    break;
                case 11: // Каждые 5 минут
                    SetCronValues("*/5", "*", "*", "*", "*");
                    break;
                case 12: // Каждые 30 минут
                    SetCronValues("*/30", "*", "*", "*", "*");
                    break;
                case 13: // Каждые 2 часа
                    SetCronValues("0", "*/2", "*", "*", "*");
                    break;
            }
            UpdateCronExpression(null, EventArgs.Empty);
        }

        private void SetCronValues(string min, string hour, string day, string month, string weekDay)
        {
            if (cmbMinutes.Items.Contains(min)) cmbMinutes.SelectedItem = min;
            if (cmbHours.Items.Contains(hour)) cmbHours.SelectedItem = hour;
            if (cmbDays.Items.Contains(day)) cmbDays.SelectedItem = day;
            if (cmbMonths.Items.Contains(month)) cmbMonths.SelectedItem = month;
            if (cmbWeekDays.Items.Contains(weekDay)) cmbWeekDays.SelectedItem = weekDay;
        }

        private void UpdateCronExpression(object sender, EventArgs e)
        {
            string minutes = cmbMinutes.SelectedItem?.ToString() ?? "*";
            string hours = cmbHours.SelectedItem?.ToString() ?? "*";
            string days = cmbDays.SelectedItem?.ToString() ?? "*";
            string months = cmbMonths.SelectedItem?.ToString() ?? "*";
            string weekDays = cmbWeekDays.SelectedItem?.ToString() ?? "*";

            CronExpression = $"{minutes} {hours} {days} {months} {weekDays}";
            lblCronResult.Text = $"Cron выражение: {CronExpression}";

            // Обновляем пояснение
            UpdateExplanation();
        }

        private void UpdateExplanation()
        {
            string minutes = cmbMinutes.SelectedItem?.ToString() ?? "*";
            string hours = cmbHours.SelectedItem?.ToString() ?? "*";
            string days = cmbDays.SelectedItem?.ToString() ?? "*";
            string months = cmbMonths.SelectedItem?.ToString() ?? "*";
            string weekDays = cmbWeekDays.SelectedItem?.ToString() ?? "*";

            string explanation = GenerateExplanation(minutes, hours, days, months, weekDays);
            txtExplanation.Text = explanation;
        }

        private string GenerateExplanation(string minutes, string hours, string days, string months, string weekDays)
        {
            var explanation = new StringBuilder();
            explanation.AppendLine("Расписание будет выполняться:");

            // Минуты
            if (minutes == "*") explanation.AppendLine("• Каждую минуту");
            else if (minutes == "0") explanation.AppendLine("• В 0 минут каждого часа");
            else if (minutes.StartsWith("*/"))
            {
                if (int.TryParse(minutes.Substring(2), out int interval))
                    explanation.AppendLine($"• Каждые {interval} минут");
                else
                    explanation.AppendLine($"• По расписанию минут: {minutes}");
            }
            else explanation.AppendLine($"• В {minutes} минут каждого часа");

            // Часы
            if (hours == "*") explanation.AppendLine("• Каждый час");
            else if (hours.StartsWith("*/"))
            {
                if (int.TryParse(hours.Substring(2), out int interval))
                    explanation.AppendLine($"• Каждые {interval} часов");
                else
                    explanation.AppendLine($"• По расписанию часов: {hours}");
            }
            else explanation.AppendLine($"• В {hours} часов");

            // Дни месяца
            if (days == "*") explanation.AppendLine("• Каждый день месяца");
            else if (days.Contains(",")) explanation.AppendLine($"• В следующие дни месяца: {days}");
            else if (days.Contains("-")) explanation.AppendLine($"• С {days.Split('-')[0]} по {days.Split('-')[1]} число месяца");
            else explanation.AppendLine($"• {days} числа каждого месяца");

            // Месяцы
            if (months == "*") explanation.AppendLine("• Каждый месяц");
            else if (months.Contains(",")) explanation.AppendLine($"• В следующие месяцы: {months}");
            else if (months.Contains("-")) explanation.AppendLine($"• С {GetMonthName(months.Split('-')[0])} по {GetMonthName(months.Split('-')[1])}");
            else explanation.AppendLine($"• В {GetMonthName(months)}");

            // Дни недели
            if (weekDays == "*") explanation.AppendLine("• Каждый день недели");
            else if (weekDays == "0") explanation.AppendLine("• По воскресеньям");
            else if (weekDays == "1-5") explanation.AppendLine("• По рабочим дням (пн-пт)");
            else if (weekDays == "0,6") explanation.AppendLine("• По выходным (сб-вс)");
            else if (weekDays.Contains(",")) explanation.AppendLine($"• В следующие дни недели: {GetWeekDaysNames(weekDays)}");
            else if (weekDays.Contains("-")) explanation.AppendLine($"• С {GetWeekDayName(weekDays.Split('-')[0])} по {GetWeekDayName(weekDays.Split('-')[1])}");
            else explanation.AppendLine($"• По {GetWeekDayName(weekDays)}");

            return explanation.ToString();
        }

        private string GetMonthName(string monthNumber)
        {
            var months = new[] { "", "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь",
            "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь" };
            return int.TryParse(monthNumber, out int index) && index >= 1 && index <= 12 ? months[index] : monthNumber;
        }

        private string GetWeekDayName(string dayNumber)
        {
            var days = new[] { "Воскресенье", "Понедельник", "Вторник", "Среда", "Четверг", "Пятница", "Суббота" };
            return int.TryParse(dayNumber, out int index) && index >= 0 && index <= 6 ? days[index] : dayNumber;
        }

        private string GetWeekDaysNames(string daysList)
        {
            var days = daysList.Split(',').Select(day => GetWeekDayName(day.Trim())).ToArray();
            return string.Join(", ", days);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!string.IsNullOrEmpty(CronExpression))
            {
                var parts = CronExpression.Split(' ');
                if (parts.Length == 5)
                {
                    if (cmbMinutes.Items.Contains(parts[0])) cmbMinutes.SelectedItem = parts[0];
                    if (cmbHours.Items.Contains(parts[1])) cmbHours.SelectedItem = parts[1];
                    if (cmbDays.Items.Contains(parts[2])) cmbDays.SelectedItem = parts[2];
                    if (cmbMonths.Items.Contains(parts[3])) cmbMonths.SelectedItem = parts[3];
                    if (cmbWeekDays.Items.Contains(parts[4])) cmbWeekDays.SelectedItem = parts[4];
                }
            }
            UpdateCronExpression(null, EventArgs.Empty);
        }
    }
}