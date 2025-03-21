# DotNet.DotEnv

Adds support for .env files for dotnet binding the environment variable names to strongly
typed static variables on one or more static class

### Get Started

Add the dependency to your project csproj

```
<PackageReference Include="DotEnv.DotNet" Version="x.x.x" OutputItemType="Analyzer" />
```

Add your .env file or files to your csproj as an AdditionalFile

```xml
<ItemGroup>
    <AdditionalFiles Update=".env">
        <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </AdditionalFiles>
</ItemGroup>
```

### Example

```dotenv
BOOL_VAR=true
STRING_VAR=ksdjlhcioub98h
INT_VAR=10
LONG_VAR=10000000000000000
FLOAT_VAR=0.99
DOUBLE_VAR=6.0
DECIMAL_VAR=10.0
```

```csharp
[DotEnvAutoGenerated]
public static partial class EnvVars
{
    public static partial bool BoolVar { get; }
    public static partial string StringVar { get; }
    public static partial int IntVar { get; }
    public static partial long LongVar { get; }
    public static partial float FloatVar { get; }
    public static partial double DoubleVar { get; }
    public static partial decimal DecimalVar { get; }
}
```

The source generator will create a partial class to bind the values in the .env files to the static class variables.

### Supported types for binding
the types shown in the example above are all the types currently supported.

### Build behaviours

#### Debug mode
If some variable cannot be bound for any reason (not existing on .env file or environment variable, existing but not able
to be converted to the expected type) your build will fail due to a compile time error saying your partial property needs to
be implemented.

#### Release mode
If some variable cannot be bound for any reason (not existing on .env file or environment variable, existing but not able
to be converted to the expected type) it will be bound to the default value.
