﻿using System;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using FourRoads.Common.TelligentCommunity.Components.Logic;
using FourRoads.TelligentCommunity.InlineContent.Controls;
using FourRoads.TelligentCommunity.InlineContent.ScriptedContentFragments;
using FourRoads.TelligentCommunity.InlineContent.Security;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Telligent.Common;
using Telligent.Common.Diagnostics.Tracing.Web;
using Telligent.DynamicConfiguration.Components;
using Telligent.Evolution.Components;
using Telligent.Evolution.Controls;
using Telligent.Evolution.Extensibility.Api.Version1;

namespace FourRoads.TelligentCommunity.InlineContent.Controls
{
    public class InlineContentControl : TraceableControl ,  IPostBackEventHandler
    {
        private InlineContentLogic _inlineContentLogic = new InlineContentLogic();
        private HtmlGenericControl _editor;
        private HtmlGenericControl _editItem;
        private HtmlAnchor _editAnchor;
        private HtmlButton _cancelButton;
        private HtmlButton _updateButton;
        private HtmlEditorStringControl _editorContent;
        private ConfigurationDataBase _configurationDataBase;

        public InlineContentControl(ConfigurationDataBase configurationDataBase)
        {
            _configurationDataBase = configurationDataBase;
        }

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            Page.ClientScript.RegisterStartupScript(GetType(), "ContentInlineDataHighlight",
            @"$(window).load(function () { 
                $('.content-inline-editor').hover( 
              function () { 
                $(this).addClass('highlight'); 
              },  
              function () { 
                $(this).removeClass('highlight');
              } 
            );});", true);
        }

        protected override void CreateChildControls()
        {
            base.CreateChildControls();

            //Add the dynamic content in here
            Controls.Add(new Literal() { Text = _inlineContentLogic.GetInlineContent(InlineContentName) ?? DefaultContent});
        }

        protected override void EnsureChildControls()
        {
            base.EnsureChildControls();

            if (!ReadOnly)
            {
                //Is the any content that is waiting to be published with a newer date, if so add an extra class
                Control[] tempCollection = new Control[Controls.Count];
                Controls.CopyTo(tempCollection, 0);
                Controls.Clear();

                HtmlGenericControl c = new HtmlGenericControl("span");
                c.ID = "content";

                c.Attributes.Add("class", "content-inline-editor");

                HtmlGenericControl ul = new HtmlGenericControl("ul");
                ul.ID = "items";
                ul.Attributes.Add("class", "content-inline-editor-buttons");

                c.Controls.Add(ul);

                //Add some button controls that are hidden
                _editItem = new HtmlGenericControl("li");
                _editItem.Attributes.Add("class", "edit");

                _editAnchor = new HtmlAnchor();
                _editAnchor.ID = "edit";
                _editAnchor.InnerHtml = "<span>Edit</span>";
                _editAnchor.HRef = "#";

                _editItem.Controls.Add(_editAnchor);
                ul.Controls.Add(_editItem);

                //Build the editor modal div
                _editor = new HtmlGenericControl("div");
                c.Controls.Add(_editor);
                _editor.Attributes.Add("style", "display:none");
                _editor.Attributes.Add("class", "fourroads-inline-content");
                _editor.ID = "editor";
                _editorContent = new HtmlEditorStringControl();
                _editorContent.ConfigurationData = _configurationDataBase;
                _editorContent.ContentTypeId = InlineContentPart.InlineContentContentTypeId;

                _editor.Controls.Add(_editorContent);

                _editorContent.Text = _inlineContentLogic.GetInlineContent(InlineContentName) ?? DefaultContent;
                _editorContent.CssClass = "editor";
                _editorContent.ID = "editorcontent";

                HtmlGenericControl actions = new HtmlGenericControl("div");
                actions.Attributes.Add("style", "float:right");
                _editor.Controls.Add(actions);

                _cancelButton = new HtmlButton();
                _cancelButton.ID = "cancel";
                _cancelButton.Attributes.Add("class", "button cancel");
                _cancelButton.InnerText = "Cancel";
                actions.Controls.Add(_cancelButton);

                _updateButton = new HtmlButton();
                _updateButton.ID = "update";
                _updateButton.Attributes.Add("class", "button update");
                _updateButton.InnerText = "Update";

                actions.Controls.Add(_updateButton);

                Controls.Add(c);

                c.Controls.Add(new HtmlGenericControl("div") { InnerText = "click to edit" });

                foreach (Control control in tempCollection)
                {
                    c.Controls.Add(control);
                }
            }
        }

        protected override void OnPreRender(EventArgs e)
        {
            if (_editAnchor != null && _editor != null && _cancelButton != null && _updateButton != null)
            {

                Page.ClientScript.RegisterClientScriptBlock(GetType(), "inlinecontent-center-func" , @"
                    jQuery.fn.inlineCenter = function ()
                    {{
                        this.css('position','fixed');
                        this.css('top', ($(window).height() / 2) - (this.outerHeight() / 2));
                        this.css('left', ($(window).width() / 2) - (this.outerWidth() / 2));
                        return this;
                    }}",true);

            Page.ClientScript.RegisterClientScriptBlock(GetType(), "inlinecontent-initialization" + ClientID, string.Format(@"
                    $(function(){{ 
                        $('#{0}').click(function(e){{
                            e.preventDefault();
                                var modal = $('#{1}');

                                var top, left;

                                top = Math.max($(window).height() - modal.outerHeight(), 0) / 2;
                                left = Math.max($(window).width() - modal.outerWidth(), 0) / 2;

                                modal.css({{
                                    top:top + $(window).scrollTop(), 
                                    left:left + $(window).scrollLeft()
                                }});

                                modal.show();
                        }})

                        $('#{2}').click(function(e){{
                            $('#{1}').hide();
                        }});

                        $('#{3}').click(function(e){{
                            e.preventDefault();
                            $(this).attr('disabled', true);
                            var args = {5};
                            {4};
                            return false;
                        }});
                   }});
                ", _editAnchor.ClientID, _editor.ClientID, _cancelButton.ClientID, _updateButton.ClientID, Page.ClientScript.GetPostBackEventReference(this, "args", false).Replace("'args'", "args"), _editorContent.GetContentScript()), true);
            }

            base.OnPreRender(e);
        }

        protected override void Render(HtmlTextWriter writer)
        {
            StringBuilder sb = new StringBuilder();
            using (HtmlTextWriter tw = new HtmlTextWriter(new System.IO.StringWriter(sb)))
            {
                base.Render(tw);
            }

            writer.Write(PublicApi.UI.Render(sb.ToString() , new UiRenderOptions()));
        }

        public string DefaultContent
        {
            get { return (string)(ViewState["DefaultContent"] ?? string.Empty); }
            set { ViewState["DefaultContent"] = value; }
        }

        public string InlineContentName
        {
            get { return (string)(ViewState["InlineContentName"] ?? string.Empty); }
            set { ViewState["InlineContentName"] = value; }
        }

        /// <summary>
        /// Gets or sets the width of the modal.
        /// </summary>
        /// <value>The width of the modal.</value>
        public int ModalWidth
        {
            get { return (int)(ViewState["ModalWidth"] ?? 640); }
            set { ViewState["ModalWidth"] = value; }
        }

        /// <summary>
        /// Gets or sets the height of the modal.
        /// </summary>
        /// <value>The height of the modal.</value>
        public int ModalHeight
        {
            get { return (int)(ViewState["ModalHeight"] ?? 510); }
            set { ViewState["ModalHeight"] = value; }
        }


        protected virtual bool ReadOnly
        {
            get { return !_inlineContentLogic.CanEdit; }
        }

        public void RaisePostBackEvent(string eventArgument)
        {
            if (!string.IsNullOrEmpty(InlineContentName))
            {
                _inlineContentLogic.UpdateInlineContent(InlineContentName, eventArgument);
            }
        }
    }
}