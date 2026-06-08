#pragma warning disable CS1591
using System;
using FlaxEngine;

namespace WsCodeEditor
{
    /// <summary>
    /// Runtime portion of the WsCodeEditor plugin.
    /// </summary>
    public class WsCodeEditorPlugin : GamePlugin
    {
        public WsCodeEditorPlugin()
        {
            _description = new PluginDescription
            {
                Name = "WsCodeEditor",
                Category = "Scripting",
                Author = "Wavestorm Software",
                RepositoryUrl = null,
                HomepageUrl = null,
                AuthorUrl = null,
                Description = "Embedded code editor tooling for Flax Engine projects.",
                Version = new Version(0, 1, 0),
                IsAlpha = true,
                IsBeta = false,
            };
        }
    }
}
