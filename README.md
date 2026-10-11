# DocMerge

DocMerge 2.1.0 ordena y transfiere filas completas de tablas DOCX y genera
documentos de clases desde históricos, listados PDF y una plantilla. Incluye
una aplicación WPF para seleccionar, revisar y generar estos lotes. La
implementación principal usa C# sobre .NET 10; la referencia Python permanece
archivada en `legacy/python/`.

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

Interfaz de escritorio:

```powershell
dotnet run --project src/DocMerge.Wpf
```

La interfaz guía por tres pasos y permite revisar clases, alumnado e incidencias
antes de elegir una carpeta nueva y publicar el lote. No edita los documentos ni
resuelve manualmente las incidencias.

```powershell
dotnet run --project src/DocMerge.Console -- "assets/documents/PAABLO ANEXOS.docx" --force
```

Generar un lote nuevo de clases:

```powershell
dotnet run --project src/DocMerge.Console -- --generar-clases `
  --historicos "assets/documents/New feature/ANEXOS" `
  --listas "assets/documents/New feature/PDF" `
  --plantilla "assets/documents/New feature/TEMPLATES/ANEXO_TEMPLATE.docx" `
  --salida "assets/outputs/clases-generadas"
```

Añade `--simular` para obtener el plan y los avisos sin crear documentos. La
carpeta de salida debe ser nueva y solo se publica después de validar el lote
completo.

## Estructura

- `src/`: código fuente C#.
- `tests/`: pruebas automatizadas C#.
- `assets/`: documentos locales privados y resultados generados.
- `legacy/python/`: referencia histórica inmutable de Python.
- `release/`: entrega actual validada para Windows x64.
- `release-desktop/`: entrega independiente de la aplicación WPF para Windows x64.
- `docs/`: documentación técnica del proyecto.

Consulta `docs/class-generation.md` para los contratos técnicos, incidencias y
flujo de mantenimiento de la generación de clases.

## Publicación Windows x64

```powershell
dotnet publish src/DocMerge.Console -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish src/DocMerge.Wpf -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
