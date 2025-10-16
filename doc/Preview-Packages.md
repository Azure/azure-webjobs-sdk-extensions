# Preview packages

This repository allows for authoring of _preview_ quality versions in parallel with _stable_ versions of the same package, from the same csproj. This is all done via the `PREVIEW` compiler constant, allowing preview-only code to go into `#if PREVIEW` sections.

## Why not `ExperimentalAttribute`?

Dotnet does support an `ExperimentalAttribute`. However, this is a compile-time only signal to the customer they are using a preview feature. Since these packages support Azure Functions, which has many non-dotnet customers that leverage an extensions 'bundle' (pre-built bundle of approved WebJobs extensions), we need a **runtime** signal to customers. Or at least, they will never see the dotnet compile time signal as they never compile any dotnet code. To address this, we will ship separate preview quality packages that can be part of the bundles _preview_ feed.

## Why not a different branch?

A different branch definitely could solve this. These preview packages are intended to be long lived and developed in parallel with the stable packages. With a different branch we would need ensure any changes to stable goes to preview, meaning constant merges and possible significant merge conflicts. This makes it harder for contributors unfamiliar with the repo as we need to tell them to port their work each time. We could automate this to an extent, but not if there are merge conflicts. Keeping everything in the same branch allows a single PR to apply to both stable and preview.

## How it works

Authoring preview scoped work is done via an MSBuild property `$(Preview)` and a compiler constant `PREVIEW`.

### MSBuild

Condition preview-only packages or entire files on the `$(Preview)` property.

``` xml
<!-- Add or update a preview-only dependency -->
<ItemGroup Condition="'$(Preview)' == 'true'">
  <!-- Update the version of a package in stable to a preview version. -->
  <PackageReference Update="Some.Package.In.Stable" Version="1.0.0-preview.1" />
  <!-- Include a new package in preview only -->
  <PackageReference Include="Some.Preview.Only.Package" Version="1.0.0" />
</ItemGroup>

<ItemGroup>
  <!-- Remove a file from stable meant only for preview -->
  <Compile Remove="SomePreviewOnlyFile.cs" Condition="'$(Preview)' != 'true'">
</ItemGroup>
```

### Code

Use if-def sections to condition on preview:

``` CSharp

// remove the property entirely if not in preview
#if PREVIEW
    public string PreviewOnlyProperty { get; set; }
#endif

// or just hide it if not in preview

#if PREVIEW
    public
#else
    internal
#endif
    string PreviewOnlyProperty { get; set; }
```

###

## Onboarding

To onboard a project to have a preview train:

1. Add the project and it's test project to [WebJobs.Extensions.Preview.proj](/eng/sln/WebJobs.Extensions.Preview.proj)
2. Add the conditional `VersionSuffix` to `Directory.Version.props`
   - ie: `<VersionSuffix Condition="'$(Preview)' == 'true'">preview.1</VersionSuffix>`
3. Update packages release CI to add a stable/preview parameter.
   - see the `quality` parameter in [official-release.cosmosdb.yml](/eng/ci/official-release.cosmosdb.yml) for an example
4. Use the MSBuild property and compiler constant as necessary to conditionally include code/packages/etc only in preview builds.
