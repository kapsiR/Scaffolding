﻿using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;

namespace Microsoft.VisualStudio.Web.CodeGeneration.MsBuild
{
    public interface IMsBuildProcessor
    {
        string Configuration { get;}
        string[] Targets { get; }
        IEnumerable<ILogger> Loggers { get; }
        Dictionary<string, string> Properties { get; }
        //void ProcessBuildResult(ProjectInstance projectInstance, IDictionary<string, TargetResult> targetResults);
        void Init();
        void ProcessBuildResult(Project _project, ProjectInstance projectStateAfterBuild, IDictionary<string, TargetResult> resultsByTarget);
    }
}