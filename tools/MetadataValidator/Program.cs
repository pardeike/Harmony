using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace MetadataValidator;

class Program
{
	static int Main(string[] args)
	{
		if (args.Length == 0)
		{
			Console.WriteLine("Usage: MetadataValidator <path-to-dll>");
			return 1;
		}

		var dllPath = args[0];
		if (!File.Exists(dllPath))
		{
			Console.WriteLine($"Error: File not found: {dllPath}");
			return 1;
		}

		Console.WriteLine($"Validating metadata for: {dllPath}");

		try
		{
			using var stream = File.OpenRead(dllPath);
			using var peReader = new PEReader(stream);
			
			if (!peReader.HasMetadata)
			{
				Console.WriteLine("Error: PE file does not contain metadata");
				return 1;
			}

			var metadataReader = peReader.GetMetadataReader();
			
			var errors = 0;

			// Validate TypeRefs
			Console.WriteLine($"Validating {metadataReader.TypeReferences.Count} type references...");
			foreach (var typeRefHandle in metadataReader.TypeReferences)
			{
				try
				{
					var typeRef = metadataReader.GetTypeReference(typeRefHandle);
					
					// Try to get the name - this is where "coded rid out of range" errors occur
					_ = metadataReader.GetString(typeRef.Name);
					_ = metadataReader.GetString(typeRef.Namespace);
					
					// Validate the resolution scope
					var resolutionScope = typeRef.ResolutionScope;
					if (!resolutionScope.IsNil)
					{
						switch (resolutionScope.Kind)
						{
							case HandleKind.AssemblyReference:
								var asmRef = metadataReader.GetAssemblyReference((AssemblyReferenceHandle)resolutionScope);
								_ = metadataReader.GetString(asmRef.Name);
								break;
							case HandleKind.ModuleReference:
								var modRef = metadataReader.GetModuleReference((ModuleReferenceHandle)resolutionScope);
								_ = metadataReader.GetString(modRef.Name);
								break;
							case HandleKind.TypeReference:
								// Nested type - recursively validate
								break;
						}
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error validating TypeRef 0x{MetadataTokens.GetToken(typeRefHandle):X8}: {ex.Message}");
					errors++;
				}
			}

			// Validate AssemblyRefs
			Console.WriteLine($"Validating {metadataReader.AssemblyReferences.Count} assembly references...");
			foreach (var asmRefHandle in metadataReader.AssemblyReferences)
			{
				try
				{
					var asmRef = metadataReader.GetAssemblyReference(asmRefHandle);
					_ = metadataReader.GetString(asmRef.Name);
					
					if (!asmRef.Culture.IsNil)
					{
						_ = metadataReader.GetString(asmRef.Culture);
					}
					
					if (!asmRef.PublicKeyOrToken.IsNil)
					{
						_ = metadataReader.GetBlobBytes(asmRef.PublicKeyOrToken);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error validating AssemblyRef 0x{MetadataTokens.GetToken(asmRefHandle):X8}: {ex.Message}");
					errors++;
				}
			}

			// Validate MemberRefs
			Console.WriteLine($"Validating {metadataReader.MemberReferences.Count} member references...");
			foreach (var memberRefHandle in metadataReader.MemberReferences)
			{
				try
				{
					var memberRef = metadataReader.GetMemberReference(memberRefHandle);
					_ = metadataReader.GetString(memberRef.Name);
					
					// Validate parent
					var parent = memberRef.Parent;
					if (!parent.IsNil)
					{
						switch (parent.Kind)
						{
							case HandleKind.TypeReference:
								var typeRef = metadataReader.GetTypeReference((TypeReferenceHandle)parent);
								_ = metadataReader.GetString(typeRef.Name);
								break;
							case HandleKind.TypeDefinition:
								var typeDef = metadataReader.GetTypeDefinition((TypeDefinitionHandle)parent);
								_ = metadataReader.GetString(typeDef.Name);
								break;
							case HandleKind.ModuleReference:
								var modRef = metadataReader.GetModuleReference((ModuleReferenceHandle)parent);
								_ = metadataReader.GetString(modRef.Name);
								break;
							case HandleKind.MethodDefinition:
								var methodDef = metadataReader.GetMethodDefinition((MethodDefinitionHandle)parent);
								_ = metadataReader.GetString(methodDef.Name);
								break;
							case HandleKind.TypeSpecification:
								// TypeSpec - skip for now
								break;
						}
					}
					
					// Validate signature
					if (!memberRef.Signature.IsNil)
					{
						_ = metadataReader.GetBlobBytes(memberRef.Signature);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error validating MemberRef 0x{MetadataTokens.GetToken(memberRefHandle):X8}: {ex.Message}");
					errors++;
				}
			}

			if (errors > 0)
			{
				Console.WriteLine($"\nValidation FAILED with {errors} error(s)");
				return 1;
			}

			Console.WriteLine("\nValidation PASSED - No metadata errors detected");
			return 0;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Fatal error: {ex.Message}");
			Console.WriteLine(ex.StackTrace);
			return 1;
		}
	}
}
