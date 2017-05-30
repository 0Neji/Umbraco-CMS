﻿using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Web.Composing;
using Umbraco.Web.Models.Trees;

namespace Umbraco.Web.Trees
{
    /// <summary>
    /// Tree for displaying partial views in the settings app
    /// </summary>
    [Tree(Constants.Applications.Settings, Constants.Trees.PartialViews, "Partial Views", sortOrder: 2)]
    public class PartialViewsTreeController : FileSystemTreeController
    {
        protected override IFileSystem FileSystem => Current.FileSystems.PartialViewsFileSystem;

        private static readonly string[] ExtensionsStatic = { "cshtml" };

        protected override string[] Extensions => ExtensionsStatic;

        protected override string FileIcon => "icon-article";

        protected override void OnRenderFolderNode(ref TreeNode treeNode)
        {
            //TODO: This isn't the best way to ensure a noop process for clicking a node but it works for now.
            treeNode.AdditionalData["jsClickCallback"] = "javascript:void(0);";
            treeNode.Icon = "icon-article";
        }

        protected override bool EnableCreateOnFolder => true;
    }
}
