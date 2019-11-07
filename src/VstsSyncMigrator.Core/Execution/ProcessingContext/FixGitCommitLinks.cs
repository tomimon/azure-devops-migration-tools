﻿using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.TeamFoundation.Git.Client;
using Microsoft.TeamFoundation;

using VstsSyncMigrator.Engine.Configuration.Processing;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using VstsSyncMigrator.Core.Execution.OMatics;

namespace VstsSyncMigrator.Engine
{
    public class FixGitCommitLinks : ProcessingContextBase
    {
        private FixGitCommitLinksConfig _config;
        private RepoOMatic _RepoOMatic;

        public FixGitCommitLinks(MigrationEngine me, FixGitCommitLinksConfig config ) : base(me, config)
        {
            _config = config;
            _RepoOMatic = new RepoOMatic(me);
        }

        public override string Name
        {
            get
            {
                return "FixGitCommitLinks";
            }
        }

        internal override void InternalExecute()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
			//////////////////////////////////////////////////
            WorkItemStoreContext targetStore = new WorkItemStoreContext(me.Target, WorkItemStoreFlags.BypassRules);
            var targetQuery = new TfsQueryContext(targetStore);
            targetQuery.AddParameter("TeamProject", me.Target.Config.Project);
            targetQuery.Query =
                string.Format(
                    @"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @TeamProject {0} ORDER BY {1}",
                    _config.QueryBit,
                    _config.OrderBit
                    );
            WorkItemCollection workitems = targetQuery.Execute();
            Trace.WriteLine(string.Format("Update {0} work items?", workitems.Count));
            //////////////////////////////////////////////////
            int current = workitems.Count;
            int count = 0;
            long elapsedms = 0;
            int noteFound = 0;
            foreach (WorkItem workitem in workitems)
            {
               
                Stopwatch witstopwatch = Stopwatch.StartNew();
				workitem.Open();

                _RepoOMatic.FixExternalGitLinks(workitem, targetStore);

                if (workitem.IsDirty)
                {
                    Trace.WriteLine($"Saving {workitem.Id}");

                    workitem.Save();
                }

                witstopwatch.Stop();
                elapsedms = elapsedms + witstopwatch.ElapsedMilliseconds;
                current--;
                count++;
                TimeSpan average = new TimeSpan(0, 0, 0, 0, (int) (elapsedms / count));
                TimeSpan remaining = new TimeSpan(0, 0, 0, 0, (int) (average.TotalMilliseconds * current));
                Trace.WriteLine(string.Format("Average time of {0} per work item and {1} estimated to completion",
                    string.Format(@"{0:s\:fff} seconds", average),
                    string.Format(@"{0:%h} hours {0:%m} minutes {0:s\:fff} seconds", remaining)));

            }
            Trace.WriteLine(string.Format("Did not find old repo for {0} links?", noteFound));
            //////////////////////////////////////////////////
            stopwatch.Stop();
            Console.WriteLine(@"DONE in {0:%h} hours {0:%m} minutes {0:s\:fff} seconds", stopwatch.Elapsed);
        }

    }
}