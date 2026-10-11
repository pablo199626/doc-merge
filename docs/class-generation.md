# Generación de documentos de clases

Estado validado el 11 de octubre de 2026. Este documento complementa
`AGENTS.md`; no sustituye los contratos históricos de ordenación y transferencia.

## Objetivo y fuentes de verdad

El caso de uso genera un documento Word por cada clase descrita por los PDF:

- Los Word históricos forman una única base global de información del alumnado.
- Los PDF deciden exclusivamente a qué clase pertenece cada alumno.
- La plantilla aporta la estructura y geometría de todos los documentos finales.
- Ningún Word actúa como destino ni tiene prioridad por curso, clase, nombre o fecha.

## Entrada y salida

El CLI requiere:

```powershell
docmerge --generar-clases `
  --historicos HISTORICOS `
  --listas PDF `
  --plantilla PLANTILLA.docx `
  --salida CARPETA_NUEVA
```

`--simular` prepara el plan y muestra incidencias sin crear documentos. El
asistente abierto sin argumentos ofrece el mismo flujo en su opción 3.

El descubrimiento es recursivo, ignora archivos temporales `~$`, excluye la
plantilla de los históricos y no atraviesa enlaces de directorio. La carpeta de
salida debe ser nueva y no admite `--force`.

## Flujo de aplicación

1. `ClassGenerationService.PrepareAsync` descubre y lee todas las entradas.
2. `PdfPigClassRosterReader` extrae clase, NIA, nombre, ordinal y localización.
3. `DocxHistoricalStudentReader` prepara cada fila y su firma conservadora.
4. `HistoricalStudentIndex` indexa todos los candidatos mediante `MatchKey`.
5. `ClassGenerationPlanner` crea planes e incidencias estructuradas.
6. `DocxClassDocumentGenerator` crea cada documento desde una copia de plantilla.
7. `DocxClassGenerationValidator` valida identidades, contenido y paquete DOCX.
8. `ClassGenerationExecutionService` vuelve a verificar hashes y publica el
   directorio completo mediante `GenerationBatchPublisher`.

La extracción semántica y la indexación no se repiten por alumno ni por clase.
Las relecturas posteriores están limitadas a cargar los paquetes usados para
generar y a comprobar hashes antes de publicar.

Todos los servicios de aplicación son independientes de consola y reutilizables
por la futura interfaz WPF.

## Identidad y ordenación

`IdentityNormalizer` conserva exactamente el contrato certificado y no debe
modificarse para este caso de uso.

`StudentNameMatchKey` es una clave adicional y exclusiva de generación:

- elimina comas;
- ignora mayúsculas y minúsculas;
- elimina acentos y equipara `ü/u`;
- conserva la diferencia entre `ñ` y `n`;
- colapsa espacios Unicode;
- no reordena palabras ni aplica coincidencia aproximada.

Una clave puede contener varios candidatos. El sistema conserva todos y solo
selecciona información cuando existe una coincidencia única o varios históricos
con la misma firma de información. El nombre visible final siempre procede del
PDF. El perfil de ordenación predeterminado es `apellidos-coma-nombre`.

## Contrato PDF de grupos

El perfil nuevo reconoce una cabecera `GRUPO` y las columnas `Orden`, `NIA`,
`Alumno`, `Edad` y `Curso`. Comprueba:

- clase única y coherente con el nombre del archivo cuando este la identifica;
- filas completas;
- ordinales consecutivos;
- curso compatible con el grupo;
- texto extraíble y paquete PDF legible.

Los PDF aprobados no declaran un total independiente. Se procesan mediante
validación estructural estricta y cada clase registra el aviso
`PdfWithoutDeclaredTotal`. No inventes un total declarado.

## Contrato DOCX

La plantilla debe tener una única tabla identificada por la cabecera exacta
`Nombre y Apellidos`, tres columnas compatibles y exactamente una fila vacía de
prototipo después de la cabecera.

La fila generada combina:

- propiedades de fila y celda de la plantilla;
- contenido enriquecido interno del histórico;
- estilos, numeración, relaciones y recursos estrictamente necesarios;
- nombre visible del PDF.

No se reconstruyen documentos ni tablas desde texto. Los alumnos ausentes o no
resueltos conservan únicamente el nombre del PDF, dejan vacías las demás
columnas y reciben sombreado `FFF2CC` en la celda del nombre.

## Incidencias

No bloquean el lote, pero se devuelven para revisión:

- `MissingHistoricalStudent`;
- `EquivalentHistoricalRecords`;
- `ConflictingHistoricalData`;
- `AmbiguousStudentIdentity`;
- `PdfWithoutDeclaredTotal`.

Las contradicciones y ambigüedades nunca copian información histórica. La futura
WPF podrá presentar y resolver estos datos estructurados antes de una revisión
final por el usuario.

Bloquean la publicación los problemas estructurales, incluidos:

- `DuplicateClass`;
- `DuplicateStudentNumber`;
- entradas ilegibles o modificadas durante la operación;
- plantilla o históricos incompatibles;
- resultado que no supera la validación.

## Código principal

| Capa | Ubicación |
| --- | --- |
| Dominio | `src/DocMerge.Domain/Generation/` |
| Aplicación | `src/DocMerge.Application/Generation/` |
| Contratos DOCX | `src/DocMerge.Application/Documents/` |
| Contratos de archivos | `src/DocMerge.Application/Files/` |
| PDF | `src/DocMerge.Infrastructure/Pdf/GroupRosterPageParser.cs` y `PdfPigClassRosterReader.cs` |
| DOCX | `src/DocMerge.Infrastructure/Docx/Docx*Generation*.cs` |
| Publicación | `src/DocMerge.Infrastructure/Files/GenerationBatchPublisher.cs` |
| Presentación | `src/DocMerge.Console/Presentation/ClassGenerationPlanFormatter.cs` |

## Límites actuales y siguientes iteraciones

- `DocMerge.Wpf` todavía no existe y no debe crearse hasta que se solicite.
- Los conflictos se representan mediante modelos e incidencias estructuradas,
  pero todavía no existe una interfaz para que el usuario seleccione o edite una
  resolución antes de regenerar.
- El CLI genera filas vacías señalizadas para ausencias, contradicciones y
  homónimos; nunca elige información dudosa.
- No se exporta aún un informe JSON independiente. La fuente reutilizable para
  una UI es `ClassGenerationPlan` junto con sus `GenerationIssue`.
- Los PDF sin total independiente conservan la limitación documentada aunque
  superen las comprobaciones estructurales.
- El entorno de validación actual no dispone de LibreOffice para renderizado
  visual automatizado; la validación implementada comprueba estructura,
  identidades, contenido, partes DOCX y publicación. Mantén una revisión visual
  en Word cuando cambie la composición de filas o la plantilla.
- La versión sigue siendo `2.1.0`; no la incrementes sin autorización expresa.

## Corpus local validado

Las entradas privadas están bajo `assets/documents/New feature/`:

- `ANEXOS/`: 7 históricos y 74 registros.
- `PDF/`: 4 listados y 94 asignaciones.
- `TEMPLATES/ANEXO_TEMPLATE.docx`: plantilla validada, SHA-256
  `7cb8ed6b167ab6bf00bdcf460317f942331cec30204ce877994945eaf32aa5cf`.

La simulación esperada actualmente obtiene 22 recuperaciones, 70 ausencias y 2
asignaciones ambiguas del mismo nombre con NIA distintos. Los resultados reales
esperados son `2A.docx`, `2B.docx`, `2C.docx` y `2D.docx`, con 24, 22, 25 y 23
alumnos respectivamente. Nunca conviertas estos recuentos en reglas de negocio.

## Verificación

Antes de cerrar cambios en este flujo:

```powershell
C:\Program Files\dotnet\dotnet.exe format DocMerge.sln --verify-no-changes --no-restore
C:\Program Files\dotnet\dotnet.exe build DocMerge.sln --no-restore -warnaserror
C:\Program Files\dotnet\dotnet.exe test DocMerge.sln --no-restore
```

Si se entrega ejecutable, publica para `win-x64`, prueba `--version`, simulación,
generación real y la opción 3 del asistente sin argumentos. Comprueba los hashes
de las entradas privadas antes y después.
