using System;
using System.CodeDom;
using System.IO;
using System.Reflection;

namespace Fonlow.Poco2Client
{
	/// <summary>
	/// Creates client code representing POCO objects
	/// </summary>
    public interface IPoco2Client
    {
		/// <summary>
		/// Creates a Code DOM which includes the specified types
		/// </summary>
		/// <param name="types">
		/// Types included in the generated Code DOM
		/// </param>
		/// <param name="methods">
		/// <see cref="CherryPicking"/> options describing how to
		/// extract types
		/// </param>
        void CreateCodeDom(Type[] types, CherryPickingMethods methods);

		/// <summary>
		/// Creates a Code DOM for the specified assembly
		/// </summary>
		/// <param name="assembly">
		/// The assembly for which to generate a Code DOM
		/// </param>
		/// <param name="methods">
		/// <see cref="CherryPicking"/> options describing how to
		/// extract types
		/// </param>
		void CreateCodeDom(Assembly assembly, CherryPickingMethods methods);

		/// <summary>
		/// Saves the generated code to the specified file
		/// </summary>
		/// <param name="fileName">
		/// Name of the file to which to save the generated code
		/// </param>
        void SaveCodeToFile(string fileName);

		/// <summary>
		/// Translates the Type Reference into one usable by the client
		/// generated code
		/// </summary>
		/// <param name="type">
		/// The type to translate
		/// </param>
		/// <returns>
		/// A reference to an equivalent type in the client generated code
		/// </returns>
        CodeTypeReference TranslateToClientTypeReference(Type type);

		/// <summary>
		/// Generates code based on the Code DOM and writes it to the
		/// specified <see cref="TextWriter"/>
		/// </summary>
		/// <param name="writer">
		/// Writer to which to write the generated code
		/// </param>
        void WriteCode(TextWriter writer);
    }
}