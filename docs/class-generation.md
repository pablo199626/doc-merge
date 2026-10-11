# Generación de documentos de clases

Motor y primera interfaz WPF validados el 11 de octubre de 2026. Este documento complementa
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

1. `ClassGenerationService.PrepareAsync` descubre y lee todas las entradas sin
   requerir todavía una carpeta de salida.
2. `PdfPigClassRosterReader` extrae clase, NIA, nombre, ordinal y localización.
3. `DocxHistoricalStudentReader` prepara cada fila y su firma conservadora.
4. `HistoricalStudentIndex` indexa todos los candidatos mediante `MatchKey`.
5. `ClassGenerationPlanner` crea planes e incidencias estructuradas.
6. `DocxClassDocumentGenerator` crea cada documento desde una copia de plantilla.
7. `DocxClassGenerationValidator` valida identidades, contenido y paquete DOCX.
8. `ClassGenerationExecutionService` recibe el mismo lote preparado que se ha
   revisado, vuelve a verificar rutas y hashes y publica el directorio completo
   mediante `GenerationBatchPublisher`.

La extracción semántica y la indexación no se repiten por alumno ni por clase.
Las relecturas posteriores están limitadas a cargar los paquetes usados para
generar y a comprobar hashes antes de publicar.

Todos los servicios de aplicación son independientes de la presentación y son
consumidos tanto por consola como por WPF.

## Interfaz WPF

`src/DocMerge.Wpf/` implementa un flujo MVVM de tres pasos: selección y análisis,
revisión de solo lectura y generación. Expone el NIA, la procedencia histórica,
los campos legibles y las incidencias globales o asociadas a clase/alumno. No
renderiza Word/PDF ni permite editar o resolver manualmente los datos.
La presentación usa controles y temas Fluent de WPF UI 4.3.0, manteniendo en
recursos propios únicamente los colores semánticos y la identidad visual.
La revisión visual validada presenta tarjetas compactas para las entradas, una
lista de clases y una tabla virtualizada con búsqueda, filtro y etiquetas de
estado. En ancho compacto, la lista de clases se sustituye por un selector y el
`DataGrid` conserva su scroll propio, sin ocultar las acciones de la pantalla.
Las tarjetas y la cabecera distinguen alumnos sin histórico, conflictos o
identidades ambiguas, avisos documentales, otros avisos y bloqueos. Los recuentos
de alumnos se deduplican por NIA dentro de cada categoría; el informe conserva
las incidencias originales y sus referencias. Los badges de alumnado activan
filtros de solo lectura y nunca cambian el plan ni sus totales.

El shell incorpora un stepper conectado y un sidebar persistente: 64 DIP
colapsado y 220 DIP expandido. En ventanas estrechas, la expansión se superpone
sobre el contenido sin reescribir la preferencia guardada. Apariencia permite
elegir directamente Sistema, Claro u Oscuro. Sistema sigue el tema de
aplicaciones de Windows; una elección `light`/`dark` del antiguo `theme.txt` se
migra sin perderla. El tema conserva el acento `#0866F5` y actualiza WPF UI y los
recursos semánticos mediante un único servicio.
El icono lateral representa siempre la preferencia seleccionada: sol para Claro,
luna para Oscuro y monitor para Sistema, aunque el tema efectivo de Sistema sea
oscuro. El lateral anima ancho y etiquetas durante 200 ms, sustituye transiciones
interrumpidas y aplica el estado final directamente cuando Windows desactiva las
animaciones.

El detalle de alumno y el informe de incidencias usan recursos dinámicos desde
su primer fotograma. Presentan identidad, clase, resolución, procedencia,
consecuencia y referencias sin permitir edición. La tabla conserva una selección
legible con y sin foco, incluso mientras el detalle modal está abierto.

La salida se elige después de revisar. La ejecución conserva el objeto
`PreparedClassGeneration` revisado y no crea silenciosamente un plan diferente.
Antes de generar y antes de publicar compara de nuevo el conjunto descubierto y
los hashes. Las carpetas temporales `.docmerge_*` quedan fuera del descubrimiento
para que una salida situada dentro del árbol histórico no invalide su propio plan.

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

Las contradicciones y ambigüedades nunca copian información histórica. La WPF
presenta estos datos estructurados para revisión, pero no permite resolverlos ni
seleccionar arbitrariamente un candidato.

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
| Presentación CLI | `src/DocMerge.Console/Presentation/ClassGenerationPlanFormatter.cs` |
| Presentación WPF | `src/DocMerge.Wpf/` |

## Límites actuales y siguientes iteraciones

- La primera WPF cubre revisión de solo lectura; edición y resolución manual
  quedan expresamente fuera de esta release.
- Los conflictos se representan mediante modelos e incidencias estructuradas;
  la interfaz los explica, pero no permite elegir o editar una resolución.
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
- La entrega WPF validada está en `release-desktop/DocMerge.exe`; la entrega de
  consola permanece separada e intacta en `release/DocMerge.exe`.
- El pulido visual descrito en este documento está validado desde código fuente,
  pero no se ha republicado. El hash documentado de `release-desktop/DocMerge.exe`
  corresponde a la entrega anterior y no acredita estos últimos cambios hasta
  que se solicite, publique y pruebe una nueva entrega.
- Se validaron visualmente selección a 1366×768 y revisión amplia y compacta con
  el corpus real. Una ejecución integral mediante la composición WPF publicó y
  validó los cuatro documentos esperados sin modificar las entradas.
- Se validaron el tema del detalle durante una modal abierta, la selección
  inactiva, la devolución de foco, el lateral colapsado/expandido y la inversión
  rápida de su transición. Queda pendiente completar manualmente la matriz
  exhaustiva de teclado, arrastrar y soltar, animaciones desactivadas desde
  Windows y DPI 125–200 %. La generación integral comprobada fue
  programática sobre la composición WPF, no una automatización de los diálogos
  nativos.

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

La verificación del 11 de octubre de 2026 superó 124 pruebas (31 WPF), build sin
avisos y formato limpio. La revisión visual ficticia cubrió los temas claro y
oscuro, las composiciones amplia y compacta a 1366×768, el lateral expandido,
el CTA de transición y la tabla rediseñada. `release-desktop/DocMerge.exe` conserva versión
`2.1.0.0`/`2.1.0`, tamaño 152.639.291 bytes y SHA-256
`58bbdc51669b940a1f08929c8e730805fd1b904c7c1d04cef6a9ca242b610429`.
