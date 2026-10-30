using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace BookmarkIndexer.Tests;

// The parts of the coding guideline that can be checked mechanically. Plain reflection, no packages.
// The test project references BookmarkIndexer.Web and BookmarkIndexer.Integrations, so it can load all four assemblies.
// To allow a deliberate exception, add the type's full name to Allowed with a comment saying why.
public class ArchitectureTests
{
    const string App = "BookmarkIndexer";

    static readonly HashSet<string> Allowed =
    [
        // "BookmarkIndexer.Core.Modules.Orders.OrderPdfBuilder", // wraps a third-party PDF API with real layout logic
    ];

    static readonly string[] BannedLibraries = ["MediatR", "AutoMapper", "FluentValidation"];
    static readonly string[] CeremonySuffixes = ["Factory", "Manager", "Helper", "Helpers", "Wrapper", "Provider", "Builder", "Utils", "Utility"];
    static readonly string[] WriteVerbs = ["Add", "Insert", "Update", "Upsert", "Delete", "Remove", "Save", "Attach"];

    static readonly Assembly Shared = Assembly.Load($"{App}.Shared");
    static readonly Assembly Core = Assembly.Load($"{App}.Core");
    static readonly Assembly Integrations = Assembly.Load($"{App}.Integrations");
    static readonly Assembly Web = Assembly.Load($"{App}.Web");
    static readonly Assembly[] All = [Shared, Core, Integrations, Web];

    [Fact]
    public void Shared_depends_on_nothing()
    {
        Pass(ForbiddenReferences(Shared, [$"{App}.", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore"]));
    }

    [Fact]
    public void Integrations_depends_only_on_Shared()
    {
        Pass(ForbiddenReferences(Integrations, [$"{App}.Core", $"{App}.Web"]));
    }

    [Fact]
    public void Core_has_no_http()
    {
        Pass(ForbiddenReferences(Core, [$"{App}.Web", "Microsoft.AspNetCore"]));
    }

    [Fact]
    public void Banned_libraries_are_not_used()
    {
        Pass(All.SelectMany(assembly => ForbiddenReferences(assembly, BannedLibraries)));
    }

    [Fact]
    public void Controllers_hubs_and_workers_do_not_touch_DbContext_or_entities()
    {
        var violations = new List<string>();
        foreach (var type in OwnTypes(Web).Where(t => t.Name.EndsWith("Controller", StringComparison.Ordinal) || t.Name.EndsWith("Hub", StringComparison.Ordinal) || t.Name.EndsWith("Worker", StringComparison.Ordinal)))
        {
            foreach (var (member, usedType) in UsedTypes(type))
            {
                if (IsDataAccess(usedType))
                {
                    violations.Add($"{type.FullName}.{member} uses {usedType.Name}. Call a Core service instead; use DTOs from Shared.");
                }
            }
        }
        Pass(violations.Distinct());
    }

    [Fact]
    public void No_ceremony_type_names()
    {
        var violations = All.SelectMany(OwnTypes)
            .Where(t => CeremonySuffixes.Any(s => BaseName(t).EndsWith(s, StringComparison.Ordinal)) || IsBasePrefixed(BaseName(t)))
            .Select(t => $"{t.FullName}: Factory/Manager/Helper/Provider/Base types need real logic. Inline it, or add it to Allowed with the reason.");
        Pass(violations);
    }

    [Fact]
    public void Only_repositories_get_interfaces_outside_Shared()
    {
        var violations = new[] { Core, Integrations, Web }.SelectMany(OwnTypes)
            .Where(t => t.IsInterface && !BaseName(t).EndsWith("Repository", StringComparison.Ordinal))
            .Select(t => $"{t.FullName}: no interface for a single implementation. Boundary interfaces for external systems go in Shared; services are concrete classes.");
        Pass(violations);
    }

    [Fact]
    public void Repositories_are_read_only_and_specific()
    {
        var violations = new List<string>();
        foreach (var type in OwnTypes(Core).Where(t => BaseName(t).EndsWith("Repository", StringComparison.Ordinal)))
        {
            if (type.IsGenericTypeDefinition)
            {
                violations.Add($"{type.FullName}: no generic repository. One repository per aggregate root with named queries.");
            }
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName))
            {
                if (WriteVerbs.Any(verb => method.Name.StartsWith(verb, StringComparison.Ordinal)))
                {
                    violations.Add($"{type.FullName}.{method.Name}: repositories only read. Write in the service with DbContext and SaveChangesAsync.");
                }
                if (Unwrap(method.ReturnType).Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IQueryable<>)))
                {
                    violations.Add($"{type.FullName}.{method.Name}: don't return IQueryable. Return List<T> of entities or DTOs.");
                }
                if (method.GetParameters().Any(p => Unwrap(p.ParameterType).Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Expression<>))))
                {
                    violations.Add($"{type.FullName}.{method.Name}: no Expression parameters. Add a named query method instead.");
                }
            }
        }
        Pass(violations);
    }

    [Fact]
    public void No_inheritance_between_own_types()
    {
        var violations = All.SelectMany(OwnTypes)
            .Where(t => t.IsClass && t.BaseType != null && All.Contains(t.BaseType.Assembly) && !typeof(Exception).IsAssignableFrom(t))
            .Select(t => $"{t.FullName} inherits {t.BaseType!.Name}: no inheritance for code reuse. Use plain methods or composition.");
        Pass(violations);
    }

    [Fact]
    public void Shared_dtos_end_with_Dto()
    {
        var violations = OwnTypes(Shared)
            .Where(t => t.IsClass && t.IsPublic && !(t.IsAbstract && t.IsSealed) && !typeof(Exception).IsAssignableFrom(t))
            .Where(t => t.Namespace != null && t.Namespace.StartsWith($"{App}.Shared.Types", StringComparison.Ordinal))
            .Where(t => !BaseName(t).EndsWith("Dto", StringComparison.Ordinal))
            .Select(t => $"{t.FullName}: DTO records in Shared/Types end with Dto.");
        Pass(violations);
    }

    static void Pass(IEnumerable<string> violations)
    {
        var list = violations.ToList();
        Assert.True(list.Count == 0, $"{list.Count} violation(s):\n" + string.Join("\n", list));
    }

    static IEnumerable<string> ForbiddenReferences(Assembly assembly, string[] forbiddenPrefixes)
    {
        return assembly.GetReferencedAssemblies()
            .Where(r => r.Name != null && forbiddenPrefixes.Any(p => r.Name.StartsWith(p, StringComparison.Ordinal)) && r.Name != assembly.GetName().Name)
            .Select(r => $"{assembly.GetName().Name} references {r.Name}");
    }

    static IEnumerable<Type> OwnTypes(Assembly assembly)
    {
        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types;
        }
        return types.OfType<Type>()
            .Where(t => !t.Name.Contains('<') && t.GetCustomAttribute<CompilerGeneratedAttribute>() == null)
            .Where(t => !Allowed.Contains(t.FullName ?? ""));
    }

    // Every type a class uses in its fields, properties, constructors and public methods.
    static IEnumerable<(string Member, Type Type)> UsedTypes(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var members = new List<(string, Type)>();
        members.AddRange(type.GetFields(flags).Where(f => !f.Name.Contains('<')).Select(f => (f.Name, f.FieldType)));
        members.AddRange(type.GetProperties(flags).Select(p => (p.Name, p.PropertyType)));
        members.AddRange(type.GetConstructors(flags).SelectMany(c => c.GetParameters()).Select(p => ("ctor", p.ParameterType)));
        foreach (var method in type.GetMethods(flags).Where(m => !m.IsSpecialName))
        {
            members.Add((method.Name, method.ReturnType));
            members.AddRange(method.GetParameters().Select(p => (method.Name, p.ParameterType)));
        }
        return members.SelectMany(m => Unwrap(m.Item2).Select(t => (m.Item1, t)));
    }

    // A type plus its element and generic argument types: Task<List<Order>> gives Task, List, Order.
    static IEnumerable<Type> Unwrap(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is { } element)
        {
            foreach (var inner in Unwrap(element))
            {
                yield return inner;
            }
        }
        if (type.IsGenericType)
        {
            foreach (var inner in type.GetGenericArguments().SelectMany(Unwrap))
            {
                yield return inner;
            }
        }
    }

    static bool IsDataAccess(Type type)
    {
        if (type.Namespace != null && (type.Namespace.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) || type.Namespace.StartsWith($"{App}.Core.Model", StringComparison.Ordinal)))
        {
            return true;
        }
        for (var t = type.BaseType; t != null; t = t.BaseType)
        {
            if (t.FullName == "Microsoft.EntityFrameworkCore.DbContext")
            {
                return true;
            }
        }
        return false;
    }

    static string BaseName(Type type)
    {
        var tick = type.Name.IndexOf('`');
        return tick < 0 ? type.Name : type.Name[..tick];
    }

    static bool IsBasePrefixed(string name)
    {
        return name.Length > 4 && name.StartsWith("Base", StringComparison.Ordinal) && char.IsUpper(name[4]);
    }
}
