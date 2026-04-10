using System.Reflection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace TNC.Trading.Platform.Infrastructure.UnitTests;

internal static class InfrastructureReflection
{
    private static readonly Lazy<Assembly[]> PlatformAssemblies = new(() =>
    [
        LoadAssembly("TNC.Trading.Platform.Infrastructure"),
        LoadAssembly("TNC.Trading.Platform.Application")
    ]);

    internal static Type GetType(string fullName) =>
        PlatformAssemblies.Value
            .Select(assembly => assembly.GetType(fullName, throwOnError: false))
            .FirstOrDefault(static type => type is not null)
        ?? throw new TypeLoadException($"Could not resolve type '{fullName}'.");

    internal static object Create(string fullName, params object?[] args) =>
        Activator.CreateInstance(
            GetType(fullName),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args,
            culture: null) ?? throw new InvalidOperationException($"Could not create {fullName}.");

    internal static object ParseEnum(string fullName, string value) =>
        Enum.Parse(GetType(fullName), value, ignoreCase: true);

    internal static object? Invoke(object target, string methodName, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(target);

        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find method {methodName} on {target.GetType().FullName}.");

        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    internal static object? InvokeStatic(string fullName, string methodName, params object?[] args)
    {
        var method = GetType(fullName).GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find static method {methodName} on {fullName}.");

        try
        {
            return method.Invoke(null, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    internal static async Task<object?> InvokeAsync(object target, string methodName, params object?[] args)
    {
        var result = Invoke(target, methodName, args);
        if (result is not Task task)
        {
            return result;
        }

        await task.ConfigureAwait(false);
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }

    internal static T GetProperty<T>(object target, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(target);

        return (T)(target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find property {propertyName} on {target.GetType().FullName}."))
            .GetValue(target)!;
    }

    internal static void SetProperty(object target, string propertyName, object? value)
    {
        ArgumentNullException.ThrowIfNull(target);

        (target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Could not find property {propertyName} on {target.GetType().FullName}."))
            .SetValue(target, value);
    }

    internal static DbContext CreateDbContext()
    {
        var dbContextType = GetType("TNC.Trading.Platform.Infrastructure.Persistence.PlatformDbContext");
        var builderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType);
        var builder = Activator.CreateInstance(builderType)
            ?? throw new InvalidOperationException("Could not create DbContextOptionsBuilder.");

        var useInMemoryDatabaseMethod = typeof(InMemoryDbContextOptionsExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
                method.Name == "UseInMemoryDatabase"
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 4);

        _ = useInMemoryDatabaseMethod
            .MakeGenericMethod(dbContextType)
            .Invoke(null, [builder, Guid.NewGuid().ToString("N"), null, null]);

        var options = builderType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Single(property => property.Name == "Options")
            .GetValue(builder)
            ?? throw new InvalidOperationException("Could not read DbContext options.");

        return (DbContext)(Activator.CreateInstance(dbContextType, options)
            ?? throw new InvalidOperationException("Could not create PlatformDbContext."));
    }

    internal static IDataProtectionProvider CreateDataProtectionProvider() =>
        DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

    internal static object CreateNullLogger(Type categoryType) =>
        Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(categoryType))
        ?? throw new InvalidOperationException($"Could not create logger for {categoryType.FullName}.");

    private static Assembly LoadAssembly(string assemblyName) =>
        AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == assemblyName)
        ?? Assembly.Load(assemblyName);
}
