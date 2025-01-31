using System.Collections.Generic;
using System.Windows.Forms;

namespace Foldda.Automation.HandlerDevKit
{
    //the "View" object of MVC
    public class HandlerView 
    {
        public const string IMAGE_CONFIG = "config";
        public const string IMAGE_TIME = "time";

        internal HandlerView(HandlerModel model)
        {
            HandlerModel = model;
        }

        internal HandlerModel HandlerModel { get; }

        public class LoggingPanel : HandlerView
        {
            internal LoggingPanel(HandlerModel model) : base(model) { }

            internal string LogText
            {
                get
                {
                    if (HandlerModel != HandlerModel.DUMMY)
                    {
                        return HandlerModel.DisplayedLogLines.Count > 0 ? string.Join("\n", HandlerModel.DisplayedLogLines) : string.Empty;
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
            }

            internal List<string> LogTextHighlightPatterns => HandlerModel.LogHighlightPatterns;
        }

        public class HandlerButtonsPanel : HandlerView
        {
            internal enum INDEX : int { LOAD=0, START=1, STOP=2, UNLOAD=3 }
            internal HandlerButtonsPanel(HandlerModel model) : base(model) { }

            internal Button[] Buttons=> HandlerModel.Buttons;
        }

        public class HandlerConfigPanel : HandlerView
        {
            internal HandlerConfigPanel(HandlerModel model) : base(model) { }

            public List<ListViewItem> HandlerParametersListViewItems
            {
                get
                {
                    List<ListViewItem> result = new List<ListViewItem>();
                    if (HandlerModel != HandlerModel.DUMMY)
                    {
                        foreach (var parameter in HandlerModel.Parameters)
                        {
                            result.Add(MakeListViewItem(HandlerParameterGroup, IMAGE_CONFIG, parameter.Name, parameter.Value));
                        }
                    }
                    return result;
                }
            }//

            public List<ListViewItem> HandlerInfoListViewItems
            {
                get
                {
                    List<ListViewItem> result = new List<ListViewItem>();
                    if (HandlerModel != HandlerModel.DUMMY)
                    {
                        result.Add(MakeListViewItem(HandlerInfoGroup, IMAGE_CONFIG, ".NET Class", HandlerModel.Handler));
                        result.Add(MakeListViewItem(HandlerInfoGroup, IMAGE_CONFIG, ".NET Assembly", HandlerModel.Assembly));
                        result.Add(MakeListViewItem(HandlerInfoGroup, IMAGE_CONFIG, "Highlight Patterns", string.Join(",", HandlerModel.LogHighlightPatterns)));
                    }

                    return result;
                }
            }


            private ListViewItem MakeListViewItem(ListViewGroup group, string icon, string heading, string value)
            {
                var item = new ListViewItem(new string[] { heading, value });
                item.ImageKey = icon;
                if (group != null) { item.Group = group; }
                return item;
            }

            public readonly ListViewGroup HandlerParameterGroup = new ListViewGroup("Handler Parameters", HorizontalAlignment.Left);
            public readonly ListViewGroup HandlerInfoGroup = new ListViewGroup("Runtime Settings", HorizontalAlignment.Left);

        }

    }

}

