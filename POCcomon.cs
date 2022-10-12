using System;
using System.Collections.Specialized;
using System.Data.SqlTypes;
using System.Runtime.Remoting.Contexts;
using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Security.Accounts;
using Sitecore.Shell.Applications.Dialogs.LayoutDetails;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI.Sheer;

namespace POC
{
    /// <summary>Represents the Set Layout Details command.</summary>
    [Serializable]
    public class POCcomon : Command
    {
        /// <summary>Executes the command in the specified context.</summary>
        /// <param name="context">The context.</param>
        /// <contract>
        ///   <requires name="context" condition="not null" />
        /// </contract>
        public override void Execute(CommandContext context)
        {
            
            Assert.ArgumentNotNull(context, "context");
            Error.AssertObject(context, "context");
            if (context.Items.Length == 1)
            {
                Item item = context.Items[0];
                NameValueCollection nameValueCollection = new NameValueCollection();
                nameValueCollection["id"] = item.ID.ToString();
                nameValueCollection["language"] = item.Language.ToString();
                nameValueCollection["version"] = item.Version.ToString();
                nameValueCollection["database"] = item.Database.Name;
                Sitecore.Context.ClientPage.Start(this, "Run", nameValueCollection);
            }
        }

        /// <summary>Queries the state of the command.</summary>
        /// <param name="context">The context.</param>
        /// <returns>The state of the command.</returns>
        public override CommandState QueryState(CommandContext context)
        {
            // Get the Role needed to skip
            Role role = Role.FromName(@"sitecore\SkipWorkflow");

            Assert.ArgumentNotNull(context, "context");
            if (context.Items.Length != 1)
            {
                return CommandState.Hidden;
            }
            Item item = context.Items[0];
            if (!HasField(item, FieldIDs.LayoutField))
            {
                return CommandState.Hidden;
            }
            // add condition to not disable the layout details if the user in role 
            if (!item.Locking.HasLock() && !Sitecore.Context.User.IsAdministrator && !Sitecore.Context.User.IsInRole(role))
            {
                return CommandState.Disabled;
            }
            if (WebUtil.GetQueryString("mode") == "preview" || !item.Access.CanWrite() || item.Appearance.ReadOnly || !item.Access.CanWriteLanguage())
            {
                return CommandState.Disabled;
            }
            return base.QueryState(context);
        }

        /// <summary>Runs the pipeline.</summary>
        /// <param name="args">The arguments.</param>
        /// <contract>
        ///   <requires name="args" condition="not null" />
        /// </contract>
        protected virtual void Run(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (!SheerResponse.CheckModified(new CheckModifiedParameters
            {
                ResumePreviousPipeline = true
            }))
            {
                return;
            }
            if (args.IsPostBack)
            {
                if (args.HasResult)
                {
                    Database database = Factory.GetDatabase(args.Parameters["database"]);
                    Assert.IsNotNull(database, "Database \"" + args.Parameters["database"] + "\" not found.");
                    Item item = database.GetItem(ID.Parse(args.Parameters["id"]), Language.Parse(args.Parameters["language"]), Sitecore.Data.Version.Parse(args.Parameters["version"]));
                    Assert.IsNotNull(item, "item");
                    LayoutDetailsDialogResult layoutDetailsDialogResult = LayoutDetailsDialogResult.Parse(args.Result);
                    ItemUtil.SetLayoutDetails(item, layoutDetailsDialogResult.Layout, layoutDetailsDialogResult.FinalLayout);
                    if (layoutDetailsDialogResult.VersionCreated)
                    {

                        Sitecore.Context.ClientPage.SendMessage(this, string.Concat("item:versionadded(id=", item.ID, ",version=", item.Version, ",language=", item.Language, ")"));
                    }
                }
            }
            else
            {
                UrlString urlString = new UrlString(UIUtil.GetUri("control:LayoutDetails"));
                urlString.Append("id", args.Parameters["id"]);
                urlString.Append("la", args.Parameters["language"]);
                urlString.Append("vs", args.Parameters["version"]);
                SheerResponse.ShowModalDialog(urlString.ToString(), "650px", string.Empty, string.Empty, response: true);
                args.WaitForPostBack();
            }
        }
    }

}