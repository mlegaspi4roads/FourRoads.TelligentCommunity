using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using FourRoads.Common.TelligentCommunity.Components;
using FourRoads.TelligentCommunity.Rules.Tokens;
using Telligent.DynamicConfiguration.Components;
using Telligent.Evolution.Controls;
using Telligent.Evolution.Extensibility;
using Telligent.Evolution.Extensibility.Api.Version1;
using Telligent.Evolution.Extensibility.Rules.Version1;
using Telligent.Evolution.Extensibility.Version1;
using Telligent.Evolution.Extensibility.Api.Entities.Version1;

namespace FourRoads.TelligentCommunity.Rules.Triggers
{
    public class ForumReplyUpdated : IRuleTrigger, ITranslatablePlugin, IConfigurablePlugin, ISingletonPlugin, ICategorizedPlugin
    {
        private static object _lockObj = new object();
        private List<string> _actions = new List<string>();
        private IRuleController _ruleController;
        private ITranslatablePluginController _translationController;
        private readonly Guid _triggerid = new Guid("{052DF652-6ADB-4B54-8965-1DFFF59D15D8}");
        private RegisterRuleTokens _ruleTokens = new RegisterRuleTokens();

        private ConcurrentDictionary<int, ForumReply>
            _beforeUpdateCache = new ConcurrentDictionary<int, ForumReply>();

        public void Initialize()
        {
            Apis.Get<IForumReplies>().Events.AfterCreate += EventsOnAfterCreate;
            Apis.Get<IForumReplies>().Events.AfterDelete += EventsOnAfterDelete;
            Apis.Get<IForumReplies>().Events.BeforeUpdate += EventsOnBeforeUpdate;
            Apis.Get<IForumReplies>().Events.AfterUpdate += EventsOnAfterUpdate;
        }

        /// <summary>
        /// Check on the state of the reply after creation
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private void EventsOnAfterCreate(ForumReplyAfterCreateEventArgs args)
        {
            try
            {
                if (_ruleController != null)
                {
                    List<string> actions = new List<string>();

                    // add in any checks in here 
                    if (args.IsAnswer.HasValue && (bool)args.IsAnswer)
                    {
                        actions.Add("Add-Answer");
                    }

                    foreach (var action in actions)
                    {
                        if (IsActionActive(action))
                        {
                            _ruleController.ScheduleTrigger(new Dictionary<string, string>()
                            {
                                {
                                    "UserId", args.Author.Id.ToString()
                                },
                                {
                                    "Id", args.Id.ToString()
                                },
                                {
                                    "Action", action
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                new TCException(
                    string.Format("EventsOnAfterCreate failed for forum reply id :{0}", args.Id),
                    ex).Log();
            }
        }

        /// <summary>
        /// Determine what has been effected by the reply being deleted
        /// </summary>
        /// <param name="forumReplyAfterDeleteEventArgs"></param>
        /// <returns></returns>
        private void EventsOnAfterDelete(ForumReplyAfterDeleteEventArgs args)
        {
            try
            {
                if (_ruleController != null)
                {
                    List<string> actions = new List<string>();
                    
                    // add in any checks in here 
                    if (args.IsAnswer.HasValue && (bool)args.IsAnswer)
                    {
                        actions.Add("Del-Answer");
                    }
                    
                    foreach (var action in actions)
                    {
                        if (IsActionActive(action))
                        {
                            _ruleController.ScheduleTrigger(new Dictionary<string, string>()
                            {
                                {
                                    "UserId", args.Author.Id.ToString()
                                },
                                {
                                    "Id", args.Id.ToString()
                                },
                                {
                                    "Action", action
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                new TCException(
                    string.Format("EventsOnAfterDelete failed for forum reply id :{0}", args.Id),
                    ex).Log();
            }
        }

        /// <summary>
        /// Save a version of the forum reply so that we can determine what actually happened 
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private void EventsOnBeforeUpdate(ForumReplyBeforeUpdateEventArgs args)
        {
            try
            {
                CacheForumReply((int)args.Id);
            }
            catch (Exception ex)
            {
                new TCException(
                    string.Format("EventsOnBeforeUpdatefailed for forum reply id :{0}", args.Id),
                    ex).Log();
            }
        }

        /// <summary>
        /// Check on the action performed
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private void EventsOnAfterUpdate(ForumReplyAfterUpdateEventArgs args)
        {
            try
            {
                if (_ruleController != null)
                {
                    int key = (int)args.Id;

                    List<string> actions = new List<string>();

                    // add in any checks in here 
                    if (_beforeUpdateCache.ContainsKey(key))
                    {
                        var old = _beforeUpdateCache[key];
                        if ((old.IsAnswer ?? false) != (args.IsAnswer ?? false))
                        {
                            if (args.IsAnswer ?? false)
                            {
                                actions.Add("Add-Answer");
                            }
                            else
                            {
                                actions.Add("Del-Answer");
                            }
                        }
                        ForumReply removed;
                        _beforeUpdateCache.TryRemove(key, out removed);
                    }
                    foreach (var action in actions)
                    {
                        if (IsActionActive(action))
                        {
                            _ruleController.ScheduleTrigger(new Dictionary<string, string>()
                            {
                                {
                                    "UserId", args.Author.Id.ToString()
                                },
                                {
                                    "Id", args.Id.ToString()
                                },
                                {
                                    "Action", action
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                new TCException(
                    string.Format("EventsOnAfterUpdate failed for forum reply id :{0}", args.Id),
                    ex).Log();
            }
        }

        /// <summary>
        /// Cache a copy of the reply before it is updated so that we can later determine what happened
        /// </summary>
        /// <param name="replyId"></param>
        /// <returns>bool</returns>
        /// 
        private bool CacheForumReply(int replyId)
        {
            if (!_beforeUpdateCache.ContainsKey(replyId))
            {
                var reply = Apis.Get<IForumReplies>().Get(replyId);
                if (!reply.HasErrors())
                {
                    _beforeUpdateCache.AddOrUpdate(replyId, reply, (key, existingVal) => reply);
                }
            }

            return true;
        }

        /// <summary>
        /// Check if the action is configured for the current rule
        /// </summary>
        /// <param name="action"></param>
        /// <returns>bool</returns>
        /// 
        private bool IsActionActive(string action)
        {
            lock (_lockObj)
            {
                if (_actions.Contains(action))
                {
                    return true;
                }
                return false;
            }
        }

        public string Name
        {
            get { return "4 Roads - Forum Reply Updated Trigger"; }
        }

        public string Description
        {
            get { return "Fires when a forum reply is added, updated or deleted"; }
        }

        public void SetController(IRuleController controller)
        {
            _ruleController = controller;
        }

        public RuleTriggerExecutionContext GetExecutionContext(RuleTriggerData data)
        {
            RuleTriggerExecutionContext context = new RuleTriggerExecutionContext();
            if (data.ContainsKey("UserId"))
            {
                int userId;

                if (int.TryParse(data["UserId"], out userId))
                {
                    var users = Apis.Get<IUsers>();
                    var user = users.Get(new UsersGetOptions() { Id = userId });

                    if (!user.HasErrors())
                    {
                        context.Add(users.ContentTypeId, user);
                        context.Add(_triggerid, true); //Added this trigger so that it is not re-entrant
                    }
                }
            }

            if (data.ContainsKey("Id"))
            {
                int replyId;

                if (int.TryParse(data["Id"], out replyId))
                {
                    var forumReplies = Apis.Get<IForumReplies>();
                    var forumReply = forumReplies.Get(replyId);

                    if (!forumReply.HasErrors())
                    {
                        context.Add(forumReply.GlobalContentTypeId, forumReply);
                    }
                }
            }

            if (data.ContainsKey("Action"))
            {
                CustomTriggerParameters ruleParameters = new CustomTriggerParameters() { Action = data["Action"] };
                context.Add(_ruleTokens.CustomTriggerParametersTypeId, ruleParameters);
            }
            return context;
        }

        public Guid RuleTriggerId
        {
            get { return _triggerid; }
        }

        public string RuleTriggerName
        {
            get { return _translationController.GetLanguageResourceValue("RuleTriggerName"); }
        }

        public string RuleTriggerCategory
        {
            get { return _translationController.GetLanguageResourceValue("RuleTriggerCategory"); }
        }

        /// <summary>
        /// Setup the contextual datatype ids for users, forum reply and custom trigger parameters
        /// custom trigger parameters are to allow config in the UI for the action being performed (suggested-answer etc)
        /// </summary>
        /// <returns>IEnumerable<Guid></returns>
        /// 
        public IEnumerable<Guid> ContextualDataTypeIds
        {
            get { return new[] { Apis.Get<IUsers>().ContentTypeId, Apis.Get<IForumReplies>().ContentTypeId, _ruleTokens.CustomTriggerParametersTypeId }; }
        }

        public void SetController(ITranslatablePluginController controller)
        {
            _translationController = controller;
        }

        public Translation[] DefaultTranslations
        {
            get
            {
                Translation[] defaultTranslation = new[] { new Translation("en-us") };

                defaultTranslation[0].Set("RuleTriggerName", "a forum reply was created, updated or deleted");
                defaultTranslation[0].Set("RuleTriggerCategory", "Forum Reply");

                return defaultTranslation;
            }
        }

        public void Update(IPluginConfiguration configuration)
        {
            lock (_lockObj)
            {
                _actions.Clear();

                string fieldList = configuration.GetCustom("Actions") ?? string.Empty;

                //Convert the string to  a list
                string[] fieldFilter = fieldList.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                _actions.AddRange(fieldFilter);
            }
        }

        public PropertyGroup[] ConfigurationOptions
        {
            get
            {
                PropertyGroup group = new PropertyGroup("Options", "Options", 0);
                Property availableFields = new Property("Actions", "Actions", PropertyType.Custom, 0, "");

                availableFields.ControlType = typeof(CheckboxListControl);
                availableFields.SelectableValues.Add(new PropertyValue("Add-Answer", "Reply accepted as answer", 0) { });
                availableFields.SelectableValues.Add(new PropertyValue("Del-Answer", "Reply rejected as answer", 0) { });

                group.Properties.Add(availableFields);

                return new[] { group };
            }
        }

        public string[] Categories
        {
            get
            {
                return new[]
                {
                    "Rules"
                };
            }
        }

    }
}
