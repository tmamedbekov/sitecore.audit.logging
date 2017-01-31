using System;
using System.Collections.Generic;
using System.Linq;
using log4net;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Items;
using Sitecore.Data.Templates;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Mvc.Pipelines.Loader;
using Sitecore.Pipelines;
using Sitecore.Publishing;
using Sitecore.Publishing.Pipelines.PublishItem;
using Sitecore.SecurityModel;
using Sitecore.Sites;

namespace Sitecore.SharedSource.Audit.Pipelines.Initialize
{
    public class Audit : InitializeRoutes
    {

        #region Configuration Entries
        // The master switch for allowing auditing; To disable Sitecore item auditing add the following to <settings> section within a patch config: <setting name="Audit.Enabled" value="false"/>
        private static readonly bool AuditingEnabled = Settings.GetBoolSetting("Audit.Enabled", true);

        // Boolean indication if this is a content authoring server (CAS) - which uses the master database.  Content delivery servers use the web database...
        private static readonly bool IsCas = SiteManager.GetSite("website").Properties["database"].ToLower() == "master";

        // To prevent duplicate item names via the shell editor (UI) add the following to <settings> section within a patch config: <setting name="PreventDuplicateItemNames" value="true"/>
        // Both auditing, and auditing created items must be enabled...
        private static readonly bool PreventDuplicateNames = Settings.GetBoolSetting("PreventDuplicateItemNames", false);

        // To disable auditing item creations, add the following to <settings> section within a patch config: <setting name="Audit.ItemCreating" value="false"/>
        private static readonly bool AuditItemCreating = Settings.GetBoolSetting("Audit.ItemCreating", true);

        // To disable auditing changes to items, add the following to <settings> section within a patch config: <setting name="Audit.ItemSaving" value="false"/>
        private static readonly bool AuditItemSaving = Settings.GetBoolSetting("Audit.ItemSaving", true);

        // To disable auditing item deletions, add the following to <settings> section within a patch config: <setting name="Audit.ItemDeleting" value="false"/>
        private static readonly bool AuditItemDeleting = Settings.GetBoolSetting("Audit.ItemDeleting", true);

        // To disable auditing copying items, add the following to <settings> section within a patch config: <setting name="Audit.ItemCopying" value="false"/>
        private static readonly bool AuditItemCopying = Settings.GetBoolSetting("Audit.ItemCopying", true);

        // To disable auditing item moves, add the following to <settings> section within a patch config: <setting name="Audit.ItemMoving" value="false"/>
        private static readonly bool AuditItemMoving = Settings.GetBoolSetting("Audit.ItemMoving", true);

        // To disable auditing renaming of items, add the following to <settings> section within a patch config: <setting name="Audit.ItemRenamed" value="false"/>
        private static readonly bool AuditItemRenamed = Settings.GetBoolSetting("Audit.ItemRenamed", true);

        // To enable auditing item sort order changes, add the following to <settings> section within a patch config: <setting name="Audit.ItemSortOrderChanged" value="true"/>
        private static readonly bool AuditItemSortOrderChanged = Settings.GetBoolSetting("Audit.ItemSortOrderChanged", false);

        // To disable auditing item template changes, add the following to <settings> section within a patch config: <setting name="Audit.ItemTemplateChanged" value="false"/>
        private static readonly bool AuditItemTemplateChanged = Settings.GetBoolSetting("Audit.ItemTemplateChanged", true);

        // To enable auditing item publish processed events, add the following to <settings> section within a patch config: <setting name="Audit.ItemPublished" value="true"/>
        private static readonly bool AuditItemPublished = Settings.GetBoolSetting("Audit.ItemPublished", false);

        #endregion


        private static ILog _log = LoggerFactory.GetLogger("Sitecore.Diagnostics.Auditing");

        public override void Process(PipelineArgs args)
        {
            OnStart();
        }

        public void Log(string message)
        {
            if (_log == null)
                Diagnostics.Log.Audit(message, this);
            else
            {
                string user = (Context.User == null) ? "extranet\\Anonymous" : Context.User.Name;
                _log.Info(string.Format("({0}): {1}", user, message));
            }
        }


        public static void OnStart()
        {
            //if (IsCas && AuditingEnabled)
            //{
                var handler = new Audit();
            if (AuditItemCreating) Event.Subscribe("item:creating", handler.OnItemCreating);
            if (AuditItemSaving) Event.Subscribe("item:saving", handler.OnItemSaving);
            if (AuditItemDeleting) Event.Subscribe("item:deleting", handler.OnItemDeleting);

            if (AuditItemCopying) Event.Subscribe("item:copying", handler.OnItemCopying);
            if (AuditItemMoving) Event.Subscribe("item:moving", handler.OnItemMoving);
            if (AuditItemRenamed) Event.Subscribe("item:renamed", handler.OnItemRenamed);
            if (AuditItemSortOrderChanged) Event.Subscribe("item:sortorderchanged", handler.OnItemSortOrderChanged);
            if (AuditItemTemplateChanged) Event.Subscribe("item:templateChanged", handler.OnItemTemplateChanged);

            if (AuditItemPublished) Event.Subscribe("publish:itemProcessed", handler.OnItemPublished);

            //}

        }

        protected void OnItemPublished(object sender, EventArgs args)
        {
            Assert.IsTrue(args != null, "args != null");
            if (args != null && args is ItemProcessedEventArgs && AuditItemPublished)
            {
                using (new SecurityDisabler())
                {
                    PublishItemContext context = (args as ItemProcessedEventArgs).Context;

                    if (context.Result.Operation == PublishOperation.Skipped)
                    {
                        try
                        {
                            // If we skipped publishing this item, we only care about logging why if we deliberately tried to republish this item...
                            if (!context.PublishOptions.CompareRevisions && context.PublishOptions.RootItem.ID == context.ItemId)
                            {
                                if (context.PublishHelper.SourceItemExists(context.ItemId))
                                {
                                    Item sourceItem = context.PublishHelper.GetSourceItem(context.ItemId);
                                    Log(string.Format("PUBLISH [{0}]: {1}", context.Result.Operation, AuditFormatter.FormatItem(sourceItem)));
                                }
                                else
                                {
                                    Log(string.Format("PUBLISH [{0}]: {1}", context.Result.Operation, context.ItemId));
                                }
                                Log(string.Format("** {0}", context.Result.Explanation));
                            }
                        }
                        catch (Exception)
                        {
                            // We don't need to log - we were skipping this item from getting published anyway
                        }
                    }
                    else
                    {
                        if (context.PublishHelper.SourceItemExists(context.ItemId))
                        {
                            Item sourceItem = context.PublishHelper.GetSourceItem(context.ItemId);
                            Log(string.Format("PUBLISH [{0}]: {1}", context.Result.Operation, AuditFormatter.FormatItem(sourceItem)));
                        }
                        else
                        {
                            Log(string.Format("PUBLISH [{0}]: {1}, msg: {2}", context.Result.Operation, context.ItemId, context.Result.Explanation));
                        }
                    }

                }
            }
        }

        /// <summary>
        /// Responds to Sitecore new item creation, cloning an item, and duplicating an item (either via the UI or API)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args">Param index 0 contains the ItemCreatingEventArgs: Contains item ID, name, master and template IDs, parent item</param>
        protected void OnItemCreating(object sender, EventArgs args)
        {
            Assert.IsTrue(args != null, "args != null");
            if (args != null && AuditItemCreating)
            {
                using (new SecurityDisabler())
                {
                    ItemCreatingEventArgs arg = Event.ExtractParameter(args, 0) as ItemCreatingEventArgs;
                    Assert.IsTrue(arg != null, "arg != null");

                    if ((arg != null) && (Context.Site.Name == "shell") && (PreventDuplicateNames))
                    {
                        foreach (Item currentItem in arg.Parent.GetChildren())
                        {
                            if ((arg.ItemName.Replace(' ', '-').ToLower() == currentItem.Name.ToLower()) && (arg.ItemId != currentItem.ID))
                            {
                                arg.Cancel = true;
                                Context.ClientPage.ClientResponse.Alert("Name \"" + currentItem.Name + "\" is already in use. Please use another name for the item.");
                                return;
                            }
                        }
                    }

                    if (arg != null && ShouldAudit(arg.Parent))
                    {
                        Item t = arg.Parent.Database.Items[arg.TemplateId];
                        string templateName = t != null ? t.Name : arg.TemplateId.ToString();

                        Log(string.Format("CREATE: {0}:{1}/{2}, id: {3}, template: {4}", arg.Parent.Database.Name, arg.Parent.Paths.Path, arg.ItemName, arg.ItemId, templateName));
                    }
                }
            }
        }

        /// <summary>
        /// Responds to Sitecore item deletions
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args">Param index 0 contains the Item being deleted</param>
        protected void OnItemDeleting(object sender, EventArgs args)
        {
            Assert.IsTrue(args != null, "args != null");
            if (args != null && AuditItemDeleting)
            {
                using (new SecurityDisabler())
                {
                    Item item = Event.ExtractParameter(args, 0) as Item;
                    Assert.IsTrue(item != null, "item != null");

                    if (item != null && ShouldAudit(item))
                    {
                        Log(string.Format("DELETE: {0}", AuditFormatter.FormatItem(item)));
                    }
                }
            }
        }

        /// <summary>
        /// Responds to Sitecore item updates
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args">Param index 0 contains the Item being saved</param>
        protected void OnItemSaving(object sender, EventArgs args)
        {
            Assert.IsTrue(args != null, "args != null");
            if (args != null && AuditItemSaving)
            {
                using (new SecurityDisabler())
                {
                    Item item = Event.ExtractParameter(args, 0) as Item;
                    Assert.IsTrue(item != null, "item != null");

                    if (item != null && ShouldAudit(item))
                    {
                        Item originalItem = item.Database.GetItem(item.ID, item.Language, item.Version);

                        var differences = FindDifferences(item, originalItem);

                        if (differences.Any())
                        {
                            TimeSpan createdTs = item.Statistics.Updated - item.Statistics.Created;
                            TimeSpan sinceLastSave = item.Statistics.Updated - originalItem.Statistics.Updated;

                            if (createdTs.TotalSeconds > 2 && sinceLastSave.TotalSeconds > 2)
                                Log(string.Format("SAVE: {0}", AuditFormatter.FormatItem(item)));

                            foreach (string f in differences)
                            {
                                    Log(string.Format("SAVE: {0}, ** [{1}]: new: {2}, old: {3}", AuditFormatter.FormatItem(item), item.Fields[f].DisplayName, item[f], string.IsNullOrWhiteSpace(originalItem[f]) ? "": originalItem[f]));
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Find non-system fields that have changed
        /// </summary>
        /// <param name="newItem"></param>
        /// <param name="originalItem"></param>
        /// <returns></returns>
        private static List<string> FindDifferences(Item newItem, Item originalItem)
        {
            newItem.Fields.ReadAll();

            IEnumerable<string> fieldNames = newItem.Fields.Select(f => f.Name).Where(name => !name.StartsWith("__"));

            return fieldNames
              .Where(fieldName => newItem[fieldName] != originalItem[fieldName] && originalItem.Fields[fieldName] != null && newItem.Fields[fieldName].ID == originalItem.Fields[fieldName].ID)
              .ToList();
        }

        private static bool ShouldAudit(Item item)
        {
            return item.Database.Name.ToLower() == "master";
        }

        /// <summary>
        /// Responds to Sitecore item copying
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args">
        /// Param index 0 contains the Item being copied,
        /// Param index 1 contains the Item Copy destination,
        /// Param index 2 contains the Result item name,
        /// Param index 3 contains the Result item ID,
        /// Param index 4 contains the boolean indication whether it is a recursive copy (including children) or not
        /// </param>
        protected void OnItemCopying(object sender, EventArgs args)
        {
            Assert.IsTrue(args != null, "args != null");
            if (args != null && AuditItemCopying)
            {
                using (new SecurityDisabler())
                {
                    Item item = Event.ExtractParameter(args, 0) as Item;
                    Assert.IsTrue(item != null, "item != null");

                    if (item != null && ShouldAudit(item))
                    {
                        Item destination = Event.ExtractParameter(args, 1) as Item;
                        string itemName = Event.ExtractParameter(args, 2) as string;
                        ID itemId = Event.ExtractParameter(args, 3) as ID;
                        bool recursive = (bool)Event.ExtractParameter(args, 4);

                        if (item.Parent.Paths.Path == destination.Paths.Path && item.Name != itemName)
                            Log( $"DUPLICATE: {item.Database.Name}:{item.Paths.Path}, destination: {destination.Paths.Path}/{itemName}, id: {itemId}{(item.Children.Count == 0 ? string.Empty : string.Format(" recursive: {0}", recursive))}");
                        else
                            Log($"COPY: {item.Database.Name}:{item.Paths.Path}, destination: {destination.Paths.Path}/{itemName}, id: {itemId}{(item.Children.Count == 0 ? string.Empty : string.Format(" recursive: {0}", recursive))}");
                    }
                }
            }
        }

        /// <summary>
        /// Responds to Sitecore item moving
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args">
        /// Param index 0 contains the Item being moved,
        /// Param index 1 contains the ID of the old parent,
        /// Param index 2 contains the ID of the new parent
        /// </param>
        protected void OnItemMoving(object sender, EventArgs args)
        {
            Assert.IsTrue(args != null, "args != null");
            if (args != null && AuditItemMoving)
            {
                using (new SecurityDisabler())
                {
                    Item item = Event.ExtractParameter(args, 0) as Item;
                    Assert.IsTrue(item != null, "item != null");

                    if (ShouldAudit(item))
                    {
                        ID oldParentId = Event.ExtractParameter(args, 1) as ID;
                        ID newParentId = Event.ExtractParameter(args, 2) as ID;
                        Item oldParent = item.Database.Items[oldParentId];
                        Item newParent = item.Database.Items[newParentId];

                        if (item != null && oldParent != null && newParent != null && oldParent.ID != newParent.ID)
                        {
                            Log(string.Format("MOVE: [{0}] from: {1}:{2} to: {3}:{4}", item.Name, oldParent.Database.Name, oldParent.Paths.Path, newParent.Database.Name, newParent.Paths.Path));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Responds to Sitecore item rename
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args">
        /// Param index 0 contains the result Item,
        /// Param index 1 contains the Item name prior to being renamed
        /// </param>
        protected void OnItemRenamed(object sender, EventArgs args)
        {
            Assert.IsTrue(args != null, "args != null");
            if (args != null && AuditItemRenamed)
            {
                using (new SecurityDisabler())
                {
                    Item item = Event.ExtractParameter(args, 0) as Item;
                    string itemNameBeforeRename = Event.ExtractParameter(args, 1) as string;

                    Assert.IsTrue(item != null, "item != null");

                    if (item != null && itemNameBeforeRename != item.Name && ShouldAudit(item))
                    {
                        Log(string.Format("RENAME: {0}:{1}/{2}, as: {3}", item.Database.Name, item.Parent.Paths.Path, itemNameBeforeRename, item.Name));
                    }
                }
            }
        }

        /// <summary>
        /// Responds to Sitecore item sort order changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args">
        /// Param index 0 contains the sorted Item,
        /// Param index 1 contains the Old sortorder value (string)
        /// </param>
        protected void OnItemSortOrderChanged(object sender, EventArgs args)
        {
            Assert.IsTrue(args != null, "args != null");
            if (args != null && AuditItemSortOrderChanged)
            {
                using (new SecurityDisabler())
                {
                    Item item = Event.ExtractParameter(args, 0) as Item;
                    string oldSortOrder = Event.ExtractParameter(args, 1) as string;

                    Assert.IsTrue(item != null, "item != null");

                    if (item != null && ShouldAudit(item))
                    {
                        Log($"SORT: {item.Database.Name}:{item.Paths.Path}, new: {item.Appearance.Sortorder}, old: {oldSortOrder}");
                    }
                }
            }
        }

        /// <summary>
        /// Responds to Sitecore item template changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args">
        /// Param index 0 contains the ID of the item being changed,
        /// Param index 1 contains the Instance of the datamanager class handling the template
        /// </param>
        protected void OnItemTemplateChanged(object sender, EventArgs args)
        {
            Assert.IsTrue(args != null, "args != null");
            if (args != null && AuditItemDeleting)
            {
                using (new SecurityDisabler())
                {
                    Item item = Event.ExtractParameter(args, 0) as Item;
                    TemplateChangeList change = Event.ExtractParameter(args, 1) as TemplateChangeList;

                    Assert.IsTrue(item != null, "item != null");

                    if (item != null && ShouldAudit(item) && change.Target.ID != change.Source.ID)
                    {
                        Log(string.Format("TEMPLATE CHANGE: {0}:{1}, target: {2}, source: {3}", item.Database.Name, item.Paths.Path, change.Target.Name, change.Source.Name));
                        foreach (TemplateChangeList.TemplateChange c in change.Changes)
                        {
                            if (c.Action == TemplateChangeAction.DeleteField)
                                Log(string.Format("** {0}: {1}", c.Action, c.SourceField.Name));
                        }
                    }
                }
            }
        }
    }
}
