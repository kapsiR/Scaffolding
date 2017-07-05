﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.Web.CodeGeneration;
using Microsoft.VisualStudio.Web.CodeGeneration.Contracts.ProjectModel;
using Microsoft.VisualStudio.Web.CodeGeneration.DotNet;
using Microsoft.VisualStudio.Web.CodeGeneration.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Dependency;

namespace Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Razor
{
    public class EFModelBasedRazorPageScaffolder : RazorPageScaffolderBase
    {
        private IEntityFrameworkService _entityFrameworkService;
        private IModelTypesLocator _modelTypesLocator;
        private static readonly IReadOnlyList<string> Views = new List<string>()
        {
            "Create",
            "Edit",
            "Details",
            "Delete",
            "List"
        };

        public EFModelBasedRazorPageScaffolder(
            IProjectContext projectContext,
            IApplicationInfo applicationInfo,
            IModelTypesLocator modelTypesLocator,
            IEntityFrameworkService entityFrameworkService,
            ICodeGeneratorActionsService codeGeneratorActionsService,
            IServiceProvider serviceProvider,
            ILogger logger)
            : base(projectContext, applicationInfo, codeGeneratorActionsService, serviceProvider, logger)
        {
            if (modelTypesLocator == null)
            {
                throw new ArgumentNullException(nameof(modelTypesLocator));
            }

            if (entityFrameworkService == null)
            {
                throw new ArgumentNullException(nameof(entityFrameworkService));
            }

            _modelTypesLocator = modelTypesLocator;
            _entityFrameworkService = entityFrameworkService;
        }

        public override async Task GenerateCode(RazorPageGeneratorModel razorGeneratorModel)
        {
            if (razorGeneratorModel == null)
            {
                throw new ArgumentNullException(nameof(razorGeneratorModel));
            }

            if (string.IsNullOrEmpty(razorGeneratorModel.ViewName))
            {
                // TODO: make a separate message resource string for this (currently setup using the one for VIEW)
                throw new ArgumentException(MessageStrings.ViewNameRequired);
            }

            if (string.IsNullOrEmpty(razorGeneratorModel.TemplateName))
            {
                throw new ArgumentException(MessageStrings.TemplateNameRequired);
            }

            if (razorGeneratorModel.NoPageModel)
            {
                // Throw not supported exception.
                throw new ArgumentException(MessageStrings.PageModelFlagNotSupported);
            }

            ModelTypeAndContextModel modelTypeAndContextModel = null;
            var outputPath = ValidateAndGetOutputPath(razorGeneratorModel, outputFileName: razorGeneratorModel.ViewName + Constants.ViewExtension);

            EFValidationUtil.ValidateEFDependencies(_projectContext.PackageDependencies);

            modelTypeAndContextModel = await ModelMetadataUtilities.ValidateModelAndGetEFMetadata(
                razorGeneratorModel,
                _entityFrameworkService,
                _modelTypesLocator,
                string.Empty);

            var layoutDependencyInstaller = ActivatorUtilities.CreateInstance<MvcLayoutDependencyInstaller>(_serviceProvider);
            await layoutDependencyInstaller.Execute();

            await GenerateView(razorGeneratorModel, modelTypeAndContextModel, outputPath);
            await layoutDependencyInstaller.InstallDependencies();

            if (modelTypeAndContextModel.ContextProcessingResult.ContextProcessingStatus == ContextProcessingStatus.ContextAddedButRequiresConfig)
            {
                throw new Exception(string.Format("{0} {1}", MessageStrings.ScaffoldingSuccessful_unregistered, MessageStrings.Scaffolding_additionalSteps));
            }
        }

        protected override IEnumerable<RequiredFileEntity> GetRequiredFiles(RazorPageGeneratorModel viewGeneratorModel)
        {
            List<RequiredFileEntity> requiredFiles = new List<RequiredFileEntity>();

            if (viewGeneratorModel.ReferenceScriptLibraries)
            {
                // TODO: make this Razor specific once we get razor content.
                requiredFiles.Add(new RequiredFileEntity(@"Views/Shared/_ValidationScriptsPartial.cshtml", @"_ValidationScriptsPartial.cshtml"));
            }

            return requiredFiles;
        }

        internal async Task GenerateViews(RazorPageGeneratorModel razorPageGeneratorModel)
        {
            if (razorPageGeneratorModel == null)
            {
                throw new ArgumentNullException(nameof(razorPageGeneratorModel));
            }

            if (razorPageGeneratorModel.NoPageModel)
            {
                // Throw not supported exception.
                throw new ArgumentException(MessageStrings.PageModelFlagNotSupported);
            }

            IDictionary<string, string> viewAndTemplateNames = new Dictionary<string, string>();
            foreach (string viewTemplate in Views)
            {
                string viewName = viewTemplate == "List" ? "Index" : viewTemplate;
                viewAndTemplateNames.Add(viewName, viewTemplate);
            }

            ModelTypeAndContextModel modelTypeAndContextModel = null;
            string outputPath = ValidateAndGetOutputPath(razorPageGeneratorModel, string.Empty);

            EFValidationUtil.ValidateEFDependencies(_projectContext.PackageDependencies);

            modelTypeAndContextModel = await ModelMetadataUtilities.ValidateModelAndGetEFMetadata(
                razorPageGeneratorModel,
                _entityFrameworkService,
                _modelTypesLocator,
                string.Empty);

            await BaseGenerateViews(viewAndTemplateNames, razorPageGeneratorModel, modelTypeAndContextModel, outputPath);
        }

        internal async Task BaseGenerateViews(IDictionary<string, string> viewsAndTemplates, RazorPageGeneratorModel razorPageGeneratorModel, ModelTypeAndContextModel modelTypeAndContextModel, string baseOutputPath)
        {
            if (viewsAndTemplates == null)
            {
                throw new ArgumentNullException(nameof(viewsAndTemplates));
            }

            if (razorPageGeneratorModel == null)
            {
                throw new ArgumentNullException(nameof(razorPageGeneratorModel));
            }

            if (modelTypeAndContextModel == null)
            {
                throw new ArgumentNullException(nameof(modelTypeAndContextModel));
            }

            if (string.IsNullOrEmpty(baseOutputPath))
            {
                baseOutputPath = ApplicationInfo.ApplicationBasePath;
            }

            foreach (KeyValuePair<string, string> entry in viewsAndTemplates)
            {
                string viewName = entry.Key;
                string templateName = entry.Value;
                string outputPath = Path.Combine(baseOutputPath, viewName + Constants.ViewExtension);
                var pageModelOutputPath = outputPath + ".cs";

                bool isLayoutSelected = !razorPageGeneratorModel.PartialView &&
                    (razorPageGeneratorModel.UseDefaultLayout || !string.IsNullOrEmpty(razorPageGeneratorModel.LayoutPage));

                RazorPageWithContextTemplateModel templateModel = GetRazorPageWithContextTemplateModel(razorPageGeneratorModel, modelTypeAndContextModel);
                templateModel.ViewName = viewName;
                templateModel.PageModelClassName = viewName + "Model";
                var pageModelTemplateName = templateName + "PageModel" + Constants.RazorTemplateExtension;
                templateName = templateName + Constants.RazorTemplateExtension;

                await _codeGeneratorActionsService.AddFileFromTemplateAsync(outputPath, templateName, TemplateFolders, templateModel);
                _logger.LogMessage($"Added Razor View : {outputPath.Substring(ApplicationInfo.ApplicationBasePath.Length)}");
                await _codeGeneratorActionsService.AddFileFromTemplateAsync(pageModelOutputPath, pageModelTemplateName, TemplateFolders, templateModel);
                _logger.LogMessage("Added PageModel : " + pageModelOutputPath.Substring(ApplicationInfo.ApplicationBasePath.Length));
            }

            await AddRequiredFiles(razorPageGeneratorModel);
        }
    }
}