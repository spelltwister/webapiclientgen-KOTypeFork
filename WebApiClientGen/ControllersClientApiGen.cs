﻿using System.Reflection;
using System.IO;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Web.Http.Description;

namespace Fonlow.Net.Http
{
    /// <summary>
    /// Generate .NET codes of the client API of the controllers
    /// </summary>
    public class ControllersClientApiGen
    {
        CodeCompileUnit targetUnit;
        Dictionary<string, object> apiClassesDic;
        CodeTypeDeclaration[] newClassesCreated;
        //  string[] prefixesOfCustomNamespaces;
        SharedContext sharedContext;
        string[] excludedControllerNames;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefixesOfCustomNamespaces">Prefixes of namespaces of custom complex data types, so the code gen will use .client of client data types.</param>
        /// <param name="excludedControllerNames">Excluse some Api Controllers from being exposed to the client API. Each item should be fully qualified class name but without the assembly name.</param>
        /// <remarks>The client data types should better be generated through SvcUtil.exe with the DC option. The client namespace will then be the original namespace plus suffix ".client". </remarks>
        public ControllersClientApiGen(string[] prefixesOfCustomNamespaces, string[] excludedControllerNames = null)
        {
            sharedContext = new SharedContext();
            sharedContext.prefixesOfCustomNamespaces = prefixesOfCustomNamespaces == null ? new string[] { } : prefixesOfCustomNamespaces;
            targetUnit = new CodeCompileUnit();
            apiClassesDic = new Dictionary<string, object>();
            this.excludedControllerNames = excludedControllerNames;
        }

        /// <summary>
        /// Save C# codes into a file.
        /// </summary>
        /// <param name="fileName"></param>
        public void SaveCSharpCode(string fileName)
        {
            CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp");
            CodeGeneratorOptions options = new CodeGeneratorOptions();
            options.BracingStyle = "C";
            using (StreamWriter sourceWriter = new StreamWriter(fileName))
            {
                provider.GenerateCodeFromCompileUnit(targetUnit, sourceWriter, options);
            }
        }

        /// <summary>
        /// Generate CodeDom of the client API for ApiDescriptions.
        /// </summary>
        /// <param name="descriptions">Web Api descriptions exposed by Configuration.Services.GetApiExplorer().ApiDescriptions</param>
        public void Generate(Collection<ApiDescription> descriptions, bool forBothAsyncAndSync = false)
        {
            var controllersGroupByNamespace = descriptions.Select(d => d.ActionDescriptor.ControllerDescriptor).Distinct().GroupBy(d => d.ControllerType.Namespace);
            foreach (var grouppedControllerDescriptions in controllersGroupByNamespace)
            {
                var clientNamespaceText = grouppedControllerDescriptions.Key + ".Client";
                var clientNamespace = new CodeNamespace(clientNamespaceText);
                targetUnit.Namespaces.Add(clientNamespace);//namespace added to Dom

                clientNamespace.Imports.AddRange(new CodeNamespaceImport[]{
                new CodeNamespaceImport("System"),
                new CodeNamespaceImport("System.Collections.Generic"),
                new CodeNamespaceImport("System.Threading.Tasks"),
                new CodeNamespaceImport("System.Net.Http"),
                new CodeNamespaceImport("Newtonsoft.Json"),
                new CodeNamespaceImport("System.Net"),

                });

                newClassesCreated = grouppedControllerDescriptions.Select(d =>
                {
                    var controllerFullName = d.ControllerType.Namespace + "." + d.ControllerName;
                    if (excludedControllerNames != null && excludedControllerNames.Contains(controllerFullName))
                        return null;

                    return CreateControllerClientClass(clientNamespace, d.ControllerName);
                }
                    ).ToArray();//add classes into the namespace
            }

            foreach (var d in descriptions)
            {
                var controllerNamespace = d.ActionDescriptor.ControllerDescriptor.ControllerType.Namespace;
                var controllerName = d.ActionDescriptor.ControllerDescriptor.ControllerName;
                var controllerFullName = controllerNamespace + "." + controllerName;
                if (excludedControllerNames != null && excludedControllerNames.Contains(controllerFullName))
                    continue;

                var existingClientClass = LookupExistingClass(controllerNamespace, controllerName);
                System.Diagnostics.Trace.Assert(existingClientClass != null);

                var apiFunction = ClientApiFunctionGen.Create(sharedContext, d, true);
                existingClientClass.Members.Add(apiFunction);
                if (forBothAsyncAndSync)
                {
                    existingClientClass.Members.Add(ClientApiFunctionGen.Create(sharedContext, d, false));
                }
            }

        }

        /// <summary>
        /// Lookup existing CodeTypeDeclaration created.
        /// </summary>
        /// <param name="namespaceText"></param>
        /// <param name="controllerName"></param>
        /// <returns></returns>
        CodeTypeDeclaration LookupExistingClass(string namespaceText, string controllerName)
        {
            for (int i = 0; i < targetUnit.Namespaces.Count; i++)
            {
                var ns = targetUnit.Namespaces[i];
                if (ns.Name == namespaceText + ".Client")
                {
                    for (int k = 0; k < ns.Types.Count; k++)
                    {
                        var c = ns.Types[k];
                        if (c.Name == controllerName)
                            return c;
                    }
                }
            }

            return null;
        }

        CodeTypeDeclaration CreateControllerClientClass(CodeNamespace ns, string className)
        {
            var targetClass = new CodeTypeDeclaration(className)
            {
                IsClass = true,
                IsPartial = true,
                TypeAttributes = TypeAttributes.Public,
            };

            ns.Types.Add(targetClass);
            AddLocalFields(targetClass);
            AddConstructor(targetClass);

            return targetClass;
        }


        void AddLocalFields(CodeTypeDeclaration targetClass)
        {
            CodeMemberField clientField = new CodeMemberField();
            clientField.Attributes = MemberAttributes.Private;
            clientField.Name = "client";
            clientField.Type = new CodeTypeReference("System.Net.Http.HttpClient");
            targetClass.Members.Add(clientField);

            CodeMemberField baseUriField = new CodeMemberField();
            baseUriField.Attributes = MemberAttributes.Private;
            baseUriField.Name = "baseUri";
            baseUriField.Type = new CodeTypeReference("System.Uri");
            targetClass.Members.Add(baseUriField);

        }

        void AddConstructor(CodeTypeDeclaration targetClass)
        {
            CodeConstructor constructor = new CodeConstructor();
            constructor.Attributes =
                MemberAttributes.Public | MemberAttributes.Final;

            // Add parameters.
            constructor.Parameters.Add(new CodeParameterDeclarationExpression(
                "System.Net.Http.HttpClient", "client"));
            constructor.Parameters.Add(new CodeParameterDeclarationExpression(
                "System.Uri", "baseUri"));

            // Add field initialization logic
            sharedContext.clientReference = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "client");
            constructor.Statements.Add(new CodeAssignStatement(sharedContext.clientReference, new CodeArgumentReferenceExpression("client")));
            sharedContext.baseUriReference = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "baseUri");
            constructor.Statements.Add(new CodeAssignStatement(sharedContext.baseUriReference, new CodeArgumentReferenceExpression("baseUri")));
            targetClass.Members.Add(constructor);
        }

    }


}