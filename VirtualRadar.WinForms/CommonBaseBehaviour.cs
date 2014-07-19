﻿// Copyright © 2013 onwards, Andrew Whewell
// All rights reserved.
//
// Redistribution and use of this software in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//    * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//    * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//    * Neither the name of the author nor the names of the program's contributors may be used to endorse or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHORS OF THE SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using VirtualRadar.Interface;

namespace VirtualRadar.WinForms
{
    /// <summary>
    /// A utility class encapsulating base behaviour common to both forms and user controls.
    /// </summary>
    class CommonBaseBehaviour
    {
        #region GetSelectedListViewTag, GetAllSelectedListViewTag, SelectListViewItemByTag, SelectListViewItemsByTags
        /// <summary>
        /// Gets the tag associated with the selected row in a list view.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="listView"></param>
        /// <returns></returns>
        public T GetSelectedListViewTag<T>(ListView listView)
            where T: class
        {
            return listView.SelectedItems.Count == 0 ? null : listView.SelectedItems[0].Tag as T;
        }

        /// <summary>
        /// Gets the tags associated with all selected items in a multi-select list view.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="listView"></param>
        /// <returns></returns>
        public T[] GetAllSelectedListViewTag<T>(ListView listView)
            where T: class
        {
            return listView.SelectedItems.OfType<ListViewItem>().Select(r => r.Tag as T).Where(r => r != null).ToArray();
        }

        /// <summary>
        /// Gets the tags associated with all checked items in a list view.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="listView"></param>
        /// <returns></returns>
        public T[] GetAllCheckedListViewTag<T>(ListView listView)
            where T: class
        {
            return listView.CheckedItems.OfType<ListViewItem>().Select(r => r.Tag as T).Where(r => r != null).ToArray();
        }

        /// <summary>
        /// Selects the list view item associated with the tag value passed across.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="listView"></param>
        /// <param name="value"></param>
        public void SelectListViewItemByTag<T>(ListView listView, T value)
            where T: class
        {
            listView.SelectedIndices.Clear();
            var item = listView.Items.OfType<ListViewItem>().Where(r => r.Tag == value).FirstOrDefault();
            if(item != null) {
                item.Selected = true;
                item.EnsureVisible();
            }
        }

        /// <summary>
        /// Selects many list view items associated with the tag values passed across.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="listView"></param>
        /// <param name="values"></param>
        /// <param name="ensureVisible"></param>
        public void SelectListViewItemsByTags<T>(ListView listView, IEnumerable<T> values, T ensureVisible = null)
            where T: class
        {
            listView.SelectedIndices.Clear();
            var tags = values.ToArray();
            if(tags.Length > 0) {
                foreach(ListViewItem item in listView.Items) {
                    var tag = item.Tag as T;
                    item.Selected = tags.Contains(tag);
                    if(tag != null && tag == ensureVisible) item.EnsureVisible();
                }
            }
        }

        /// <summary>
        /// Sets the checked state of every list view item.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="listView"></param>
        /// <param name="checkedValues"></param>
        public void CheckListViewItemsByTags<T>(ListView listView, IEnumerable<T> checkedValues)
            where T: class
        {
            var tags = checkedValues.ToArray();
            foreach(ListViewItem item in listView.Items) {
                var tag = item.Tag as T;
                var ticked = tags.Contains(tag);
                if(ticked != item.Checked) item.Checked = ticked;
            }
        }
        #endregion

        #region FillDropDownWithEnumValues, FillDropDownWithValues, GetSelectedComboBoxValue, SelectComboBoxItemByValue
        /// <summary>
        /// Fills the dropdown list for a combo box with enum values.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="comboBox"></param>
        /// <param name="converter"></param>
        /// <param name="orderByDescription"></param>
        public void FillDropDownWithEnumValues<T>(ComboBox comboBox, TypeConverter converter, bool orderByDescription = true)
        {
            var items = Enum.GetValues(typeof(T))
                            .OfType<T>()
                            .Select(r => new ValueDescription<T>(r, converter.ConvertToString(r)))
                            .ToList();
            if(orderByDescription) items.Sort((lhs, rhs) => String.Compare(lhs.Description, rhs.Description));

            FillDropDownList<T>(comboBox, items);
        }

        /// <summary>
        /// Fills the dropdown list for a combo box with values.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="comboBox"></param>
        /// <param name="values"></param>
        /// <param name="getDescription"></param>
        public void FillDropDownWithValues<T>(ComboBox comboBox, IEnumerable<T> values, Func<T, string> getDescription)
        {
            var items = values.Select(r => new ValueDescription<T>(r, getDescription(r)))
                              .ToList();
            FillDropDownList<T>(comboBox, items);
        }

        private static void FillDropDownList<T>(ComboBox comboBox, List<ValueDescription<T>> items)
        {
            comboBox.Items.Clear();
            comboBox.DisplayMember = "Description";
            comboBox.ValueMember = "Value";
            foreach (var item in items) {
                comboBox.Items.Add(item);
            }
        }

        /// <summary>
        /// Returns the value associated with the selected item in a combo box.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="comboBox"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public T GetSelectedComboBoxValue<T>(ComboBox comboBox, T defaultValue = default(T))
        {
            T result = defaultValue;

            if(comboBox.SelectedIndex != -1) result = ((ValueDescription<T>)comboBox.SelectedItem).Value;

            return result;
        }

        /// <summary>
        /// Selects the item in a combo box associated with the value passed across.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="comboBox"></param>
        /// <param name="value"></param>
        public void SelectComboBoxItemByValue<T>(ComboBox comboBox, T value)
        {
            var selectItem = comboBox.Items.OfType<ValueDescription<T>>().FirstOrDefault(r => r.Value.Equals(value));
            comboBox.SelectedItem = selectItem;
        }

        /// <summary>
        /// Returns a collection of all of the combo box values.
        /// </summary>
        /// <param name="comboBox"></param>
        /// <returns></returns>
        public IEnumerable<T> GetComboBoxValues<T>(ComboBox comboBox)
        {
            return comboBox.Items.OfType<ValueDescription<T>>().Select(r => r.Value);
        }
        #endregion

        #region PopulateListView, FillListViewItem, FindListViewItemForRecord
        /// <summary>
        /// Populates a list view, assigning the record to Tag and reselecting the currently selected record if applicable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="listView"></param>
        /// <param name="records"></param>
        /// <param name="selectedRecord"></param>
        /// <param name="populateListViewItem"></param>
        /// <param name="selectRecord"></param>
        public void PopulateListView<T>(ListView listView, IEnumerable<T> records, T selectedRecord, Action<ListViewItem> populateListViewItem, Action<T> selectRecord)
        {
            listView.Items.Clear();
            foreach(var record in records) {
                var item = new ListViewItem() { Tag = record };
                populateListViewItem(item);
                listView.Items.Add(item);
            }

            if(selectRecord != null && records.Contains(selectedRecord)) selectRecord(selectedRecord);
        }

        /// <summary>
        /// Fills a list view item with text columns on the assumption that the item's tag contains a reference to a record.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <param name="extractColumnText"></param>
        public void FillListViewItem<T>(ListViewItem item, Func<T, string[]> extractColumnText)
            where T: class
        {
            FillAndCheckListViewItem<T>(item, extractColumnText, null);
        }

        /// <summary>
        /// Fills a list view item with text columns and sets the Checked state on the assumption that the item's tag contains a reference to a record.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <param name="extractColumnText"></param>
        /// <param name="extractChecked"></param>
        public void FillAndCheckListViewItem<T>(ListViewItem item, Func<T, string[]> extractColumnText, Func<T, bool> extractChecked)
            where T: class
        {
            var record = (T)item.Tag;
            var columnText = extractColumnText(record);
            var ticked = extractChecked == null ? false : extractChecked(record);

            while(item.SubItems.Count < columnText.Length) item.SubItems.Add("");
            for(var i = 0;i < columnText.Length;++i) {
                item.SubItems[i].Text = columnText[i];
            }

            if(extractChecked != null) item.Checked = ticked;
        }

        /// <summary>
        /// Returns the list view item whose tag is the same as the record passed across.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="listView"></param>
        /// <param name="record"></param>
        /// <returns></returns>
        public ListViewItem FindListViewItemForRecord<T>(ListView listView, T record)
            where T: class
        {
            return record == null ? null : listView.Items.OfType<ListViewItem>().Where(r => r.Tag == record).FirstOrDefault();
        }
        #endregion

        #region ParseNInt
        /// <summary>
        /// Parses a nullable integer, returning null if the text is unparseable.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public int? ParseNInt(string text)
        {
            int result;
            return int.TryParse(text, out result) ? result : (int?)null;
        }
        #endregion

        #region GetAllChildControls
        /// <summary>
        /// Returns a recursive collection of every control under the control passed across, optionally including the top-level control.
        /// </summary>
        /// <param name="topLevel"></param>
        /// <param name="includeTopLevel"></param>
        /// <returns></returns>
        public List<Control> GetAllChildControls(Control topLevel, bool includeTopLevel = false)
        {
            var result = new List<Control>();
            AddChildControls(result, topLevel, includeTopLevel);

            return result;
        }

        private void AddChildControls(List<Control> controls, Control control, bool includeSelf)
        {
            if(includeSelf) controls.Add(control);
            foreach(Control child in control.Controls) {
                AddChildControls(controls, child, true);
            }
        }
        #endregion

        #region Binding helpers - AddBinding, GetAllBindings, GetAllDataBindingsForAttribute, GetPropertyInfoForBinding
        /// <summary>
        /// Adds a binding between a control and a property on a page.
        /// </summary>
        /// <typeparam name="TControl"></typeparam>
        /// <typeparam name="TModel"></typeparam>
        /// <param name="model"></param>
        /// <param name="control"></param>
        /// <param name="modelProperty"></param>
        /// <param name="controlProperty"></param>
        /// <param name="dataSourceUpdateMode"></param>
        public System.Windows.Forms.Binding AddBinding<TControl, TModel>(TModel model, TControl control, Expression<Func<TModel, object>> modelProperty, Expression<Func<TControl, object>> controlProperty, DataSourceUpdateMode dataSourceUpdateMode)
            where TControl: Control
        {
            var controlPropertyName = PropertyHelper.ExtractName<TControl>(controlProperty);
            var modelPropertyName = PropertyHelper.ExtractName<TModel>(modelProperty);

            var result = control.DataBindings.Add(controlPropertyName, model, modelPropertyName);
            result.DataSourceUpdateMode = dataSourceUpdateMode;

            return result;
        }

        /// <summary>
        /// Returns a list of all of the bindings on the control passed across and every control under it.
        /// </summary>
        /// <param name="control"></param>
        /// <param name="includeChildControls"></param>
        /// <returns></returns>
        public List<System.Windows.Forms.Binding> GetAllDataBindings(Control control, bool includeChildControls)
        {
            var allControls = includeChildControls ? (IEnumerable<Control>)GetAllChildControls(control, true) : new Control[] { control };
            var result = allControls.SelectMany(r => r.DataBindings.OfType<System.Windows.Forms.Binding>()).ToList();

            return result;
        }

        /// <summary>
        /// Returns a list of all bindings associated with properties that are tagged with the supplied attribute type.
        /// The tag is the first instance of each attribute type associated with the property.
        /// </summary>
        /// <typeparam name="T">The type of attribute to search for.</typeparam>
        /// <param name="control">The top-level control to search. This control and all children will be searched.</param>
        /// <param name="includeChildControls"></param>
        /// <param name="inherit"></param>
        /// <returns></returns>
        public List<BindingTag<T>> GetAllDataBindingsForAttribute<T>(Control control, bool includeChildControls, bool inherit)
            where T:Attribute
        {
            var result = new List<BindingTag<T>>();

            var allBindings = GetAllDataBindings(control, includeChildControls);
            foreach(var binding in allBindings) {
                var propertyInfo = GetPropertyInfoForBinding(binding);
                var attribute = propertyInfo == null ? null : propertyInfo.GetCustomAttributes(typeof(T), inherit).OfType<T>().FirstOrDefault();
                if(attribute != null) {
                    result.Add(new BindingTag<T>(binding, attribute));
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the property info associated with a binding or null if it cannot be found.
        /// </summary>
        /// <param name="binding"></param>
        /// <returns></returns>
        public PropertyInfo GetPropertyInfoForBinding(System.Windows.Forms.Binding binding)
        {
            PropertyInfo result = null;
            if(binding.BindingMemberInfo != null && binding.DataSource != null) {
                if(!String.IsNullOrEmpty(binding.BindingMemberInfo.BindingPath)) throw new NotImplementedException("Need to implement support for binding to child objects");
                result = binding.DataSource.GetType().GetProperty(binding.BindingMemberInfo.BindingField);
            }

            return result;
        }
        #endregion
    }
}
