# DocMerge

DocMerge 2.1.0 ordena y transfiere filas completas de tablas DOCX
usando un listado PDF como referencia. La implementación principal usa C# sobre
.NET 10; la referencia Python permanece archivada en `legacy/python/`.

El comando se identifica como `docmerge` y la entrega para Windows se publica
como `DocMerge.exe`.

## Desarrollo

```powershell
dotnet build DocMerge.sln -warnaserror
dotnet test DocMerge.sln
```

Si el `dotnet` x86 instalado en el equipo aparece antes en `PATH`, usa
`C:\Program Files\dotnet\dotnet.exe`.

## Ejecución

```powershell
dotnet run --project src/DocMerge.Console -- "assets/documents/PAABLO ANEXOS.docx" --force
```

## Estructura

- `src/`: código fuente C#.
- `tests/`: pruebas automatizadas C#.
- `assets/`: documentos locales privados y resultados generados.
- `legacy/python/`: referencia histórica inmutable de Python.
- `release/`: entrega certificada para Windows x64.
- `docs/`: documentación técnica del proyecto.

## Publicación Windows x64

```powershell
dotnet publish src/DocMerge.Console -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
