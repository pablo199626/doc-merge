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
3. `DocxHistoricalStudentReader` prepara cada fila y sus campos de revisión.
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
revisión de solo lectura y configuración/generación. El paso de revisión continúa
con «Configurar documentos» y nunca escribe archivos; «Generar documentos» es la
única acción que ejecuta el lote. Expone el NIA, la procedencia histórica,
los campos legibles y las incidencias globales o asociadas a clase/alumno. No
renderiza el documento Word completo ni el PDF, pero prepara y muestra una
vista enriquecida de las medidas históricas. No permite editar ni resolver
manualmente los datos.
La presentación comercial se identifica como **Talos — Automatización
documental** y usa controles y temas Fluent de WPF UI 4.3.0, manteniendo en
recursos propios únicamente los colores semánticos y la identidad visual.
La revisión visual validada presenta tarjetas compactas para las entradas, una
lista de clases y una tabla virtualizada con búsqueda, filtro y etiquetas de
estado. En ancho compacto, la lista de clases se sustituye por un selector y el
`DataGrid` conserva su scroll propio, sin ocultar las acciones de la pantalla.
Las tarjetas y la cabecera distinguen los estados Encontrado, Alumno nuevo y
Conflicto, además de los bloqueos estructurales. Los recuentos usan asignaciones
del PDF y separan alumnado del listado, incluido y omitido. Los badges de
alumnado activan filtros de solo lectura y nunca cambian el plan ni sus totales.

El shell incorpora un stepper conectado y un sidebar persistente: 64 DIP
colapsado y 220 DIP expandido. En ventanas estrechas, la expansión se superpone
sobre el contenido sin reescribir la preferencia guardada. Apariencia permite
elegir directamente Sistema, Claro u Oscuro. Sistema sigue el tema de
aplicaciones de Windows; una elección `light`/`dark` del antiguo `theme.txt` se
migra sin perderla. El tema aplica la identidad bronce (`#C98A3F` y `#F6CF8E`)
y actualiza WPF UI y los recursos semánticos mediante un único servicio. Los
estados de información, éxito, advertencia y error conservan colores propios.
El icono lateral representa siempre la preferencia seleccionada: sol para Claro,
luna para Oscuro y monitor para Sistema, aunque el tema efectivo de Sistema sea
oscuro. El lateral anima ancho y etiquetas durante 200 ms, sustituye transiciones
interrumpidas y aplica el estado final directamente cuando Windows desactiva las
animaciones.

El detalle de alumno está integrado en la revisión y es independiente de la modal
reservada a los informes generales de incidencias. Se acopla como una tercera
columna de 440 DIP al borde derecho y ocupa toda la altura útil. Las tres zonas son
hijos directos de `ReviewWorkspace`: el detalle no pertenece a `ReviewTableSurface`.
Con 1500 DIP útiles se muestran clases (260 DIP), tabla (espacio restante) y detalle.
Por debajo se compactan las clases; con menos de 1040 DIP útiles el detalle pasa a
vista principal temporal con «Volver al listado», conservando la instancia de la
tabla, sus filtros y selección. La transición de 200 ms modifica progresivamente
el espacio reservado al detalle, recortado por el área de revisión, sin superponerlo
a la tabla. Cabecera, resultado y navegación inferior permanecen fijos; solo el
cuerpo tiene scroll. Cambiar de clase, excluir
mediante filtros al alumno visible o abandonar la revisión cierra y descarta el
detalle.

La cabecera fija presenta identidad, clase, NIA, estado, posición dentro de los
resultados visibles y navegación anterior/siguiente. La navegación sigue
exactamente `VisibleStudents`, por lo que respeta búsqueda, filtro y orden. La
activación por fila, Enter o «Ver detalle» conserva las protecciones frente a
scrollbars y arrastre; al cerrar se restaura la tabla o el botón de origen. Escape
cierra primero el detalle y las animaciones se omiten cuando Windows las desactiva.

«Medidas del alumno» combina en orden las celdas históricas no identificativas y
no vacías. `DocxHistoricalStudentReader` prepara durante la única lectura inicial
una proyección inmutable de párrafos, runs, saltos, formato de carácter, alineación
y numeración básica. La WPF la representa sobre una superficie clara tipo papel,
también en tema oscuro, sin releer documentos ni convertirlos a RTF. El
WordprocessingML original de `DocumentRecord.RowXml` continúa siendo la única
fuente para generación. Imágenes, objetos y otros elementos no representables se
señalan explícitamente en lugar de omitirse en silencio.

Encontrado muestra las medidas del histórico único; Alumno nuevo presenta «No hay
medidas registradas»; Conflicto conserva la omisión y permite recorrer candidatos
solo para inspección, sin modificar `SelectedCandidate`. Procedencia e incidencias
usan datos estructurados. La apertura de un PDF o DOCX original exige una acción
explícita, valida ruta, existencia y extensión y no cambia el plan.

La salida se elige después de revisar. La ejecución conserva el objeto
`PreparedClassGeneration` revisado y no crea silenciosamente un plan diferente.
Antes de generar y antes de publicar compara de nuevo el conjunto descubierto y
los hashes. Las carpetas temporales `.docmerge_*` quedan fuera del descubrimiento
para que una salida situada dentro del árbol histórico no invalide su propio plan.

La WPF representa la sesión mediante un estado exclusivo. Cancelar solo aparece
mientras la operación puede aceptar la solicitud; después muestra «Cancelando…»
y espera al retorno y limpieza reales. Al entrar en guardado final deja de ofrecer
cancelación. Si el movimiento atómico ya terminó, el resultado sigue siendo éxito
aunque exista una solicitud tardía: nunca se eliminan documentos finales para
simular una cancelación. Cerrar la ventana durante una operación aplica la misma
espera y los intentos de cierre repetidos no eluden la limpieza.

El tercer paso permite editar, para todas las clases que producirán un documento,
un título visible y el tutor o tutora opcional. Los valores se asocian a
`ClassIdentifier`, no al texto
editable ni a la posición de una fila, y se capturan en una instantánea antes de
ejecutar. Navegar entre revisión y generación conserva el borrador. Volver desde
la revisión a los archivos descarta el plan, filtros, cabeceras y configuración
de salida, pero conserva las rutas seleccionadas; «Analizar y continuar» siempre
relee las entradas actuales y construye un plan nuevo. Esta edición no relee
alumnado ni modifica el matching.

Un resultado confirmado sustituye el formulario por un resumen inmutable de la
ejecución: documentos, alumnado incluido y omitido, incidencias, cabeceras y ruta proceden de la
solicitud y el resultado ejecutados. Un fallo al abrir esa carpeta se presenta por
separado y no invalida la generación. «Nueva generación» vacía históricos, PDF,
salida, filtros, cabeceras y resultado, restaura la plantilla incluida y conserva
solo las preferencias visuales. No modifica ni elimina archivos del usuario.

La WPF selecciona inicialmente la «Plantilla estándar de Talos». Su identidad
incluida y su estado de validación documental se muestran por separado. Puede
reemplazarse por una plantilla externa y restaurarse; cualquiera de esos cambios
obliga a volver a selección y validar un plan nuevo. La ruta privada de la copia
de trabajo incluida no se presenta al usuario.

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
selecciona información cuando existe exactamente una coincidencia histórica. Dos
o más candidatos constituyen un conflicto aunque su contenido sea idéntico. El
nombre visible final siempre procede del PDF. El perfil de ordenación
predeterminado es `apellidos-coma-nombre`.

## Contrato PDF de grupos

El perfil nuevo reconoce una cabecera `GRUPO` y las columnas `Orden`, `NIA`,
`Alumno`, `Edad` y `Curso`. Comprueba:

- clase única y coherente con el nombre del archivo cuando este la identifica;
- filas completas;
- ordinales consecutivos;
- curso compatible con el grupo;
- texto extraíble y paquete PDF legible.

Los PDF aprobados no declaran un total independiente. Se procesan mediante
validación estructural estricta sin inventar un total ni registrar una incidencia
por su ausencia.

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
columnas y conservan el formato neutro de la fila prototipo, sin sombreado especial.

### Cabecera visible por clase

Una plantilla que admita personalización contiene exactamente dos controles de
contenido de texto simple en párrafos distintos de la misma celda del bloque
inicial del cuerpo, antes de la tabla de alumnado:

- `DocMerge.ClassTitle` para el título visible;
- `DocMerge.TutorName` para el tutor o tutora opcional.

Los controles no pueden estar en encabezados técnicos, pies, cuadros de texto,
filas repetidas ni dentro de la tabla de alumnado. La generación cambia solo sus
valores y conserva su formato. Un tutor vacío mantiene el párrafo y el espacio
previsto sin introducir un placeholder. La validación compara el resto del
documento y rechaza modificaciones no autorizadas.

Una plantilla externa sin ninguna etiqueta reservada sigue siendo compatible con
la generación anterior y deshabilita la edición. Una sola etiqueta, duplicados o
una ubicación inválida hacen incompatible la plantilla para evitar una
personalización parcial.

El título es obligatorio, no admite saltos ni controles y tiene un máximo de 60
caracteres percibidos. El tutor es opcional, con máximo de 120. El texto no vacío
se conserva literalmente; no se trunca ni se cambia de caja. Para las clases ESO,
el valor inicial legible se forma como `2º ESO C` sin cambiar `2ESOC`, que continúa
siendo la identidad estable. En la plantilla oficial, título y tutor ocupan líneas
consecutivas centradas, manteniendo vacío el segundo párrafo cuando no se indica tutor.

### Plantilla oficial incluida

Infrastructure incorpora como recurso `StandardClassTemplate.v1.docx`:

- identificador `standard-class`;
- versión de plantilla `1`;
- formato `academic-measures-three-column`;
- contrato de cabecera `1`;
- SHA-256 `f70a3e38d42268eda04c92f0442f2c489c2ce57bb033c042b7b9c8200baa9670`.

El recurso público fue preparado y saneado sin alterar el original privado. En
ejecución se materializa una copia verificada bajo el perfil local del usuario,
con bloqueo por sesión y limpieza al cerrar. No se escribe en la instalación ni
se requiere el repositorio, Internet o permisos administrativos. La copia queda
sometida a los mismos controles de estructura e integridad que una plantilla
externa.

## Incidencias

`DuplicateStudentName` no bloquea el lote, pero se devuelve para revisión cuando
un alumno del PDF tiene varios registros históricos o cuando el mismo nombre
corresponde a distintos NIA. Todos los alumnos afectados adoptan el estado
Conflicto y se omiten de la salida. La WPF presenta candidatos y referencias,
pero todavía no permite resolver el conflicto ni seleccionar un registro.

Alumno nuevo es un estado ordinario y no crea una incidencia.

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
  del alumnado quedan expresamente fuera de esta release; solo se editan los dos
  valores de cabecera de salida descritos arriba.
- El visor enriquecido es deliberadamente unidireccional. Conserva con fidelidad
  razonable texto, párrafos, negrita, cursiva, subrayado, fuente, tamaño, color,
  resaltado, alineación y listas básicas, pero no pretende reproducir paginación,
  métricas exactas de Word, fuentes ausentes, objetos embebidos ni numeración
  avanzada. La futura edición requerirá una transformación explícita de vuelta a
  WordprocessingML; no se presupone reversibilidad de `FlowDocument` o RTF.
- Los conflictos se representan mediante modelos e incidencias estructuradas;
  la interfaz los explica, pero no permite elegir o editar una resolución.
- El CLI y la WPF generan filas neutras para alumnos nuevos. Los conflictos se
  omiten completamente; si una clase queda sin alumnos incluidos, no se genera
  su archivo. Nunca se elige información dudosa ni se añade sombreado especial.
- No se exporta aún un informe JSON independiente. La fuente reutilizable para
  una UI es `ClassGenerationPlan` junto con sus `GenerationIssue`.
- Los PDF sin total independiente conservan la limitación documentada aunque
  superen las comprobaciones estructurales; esta limitación no es una incidencia.
- El entorno de validación actual no dispone de LibreOffice para renderizado
  visual automatizado; la validación implementada comprueba estructura,
  identidades, contenido, partes DOCX y publicación. Mantén una revisión visual
  en Word cuando cambie la composición de filas o la plantilla.
- La versión técnica compartida y la consola siguen siendo `2.1.0`. La WPF
  sobrescribe exclusivamente sus metadatos comerciales como Talos `1.0.0`, sin
  cambiar contratos ni identificadores internos.
- La entrega WPF comercial se publica en `release-desktop/Talos.exe`; la entrega
  de consola permanece separada e intacta en `release/DocMerge.exe`.
- El pulido visual y de estados descrito en este documento está publicado en la
  entrega WPF actual. La prueba aislada confirmó que no necesita una plantilla
  instalada junto al ejecutable: materializa y verifica el recurso incrustado.
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

La simulación esperada actualmente obtiene 22 alumnos encontrados, 70 alumnos
nuevos y 2 conflictos del mismo nombre con NIA distintos. Los documentos
previstos son `2A.docx`, `2B.docx`, `2C.docx` y `2D.docx`, con 23, 22, 24 y 23
alumnos incluidos respectivamente; las dos asignaciones en conflicto permanecen
en la revisión y se omiten de los DOCX. Nunca conviertas estos recuentos en reglas
de negocio.

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
el CTA de transición y la tabla rediseñada. Aquella entrega histórica tenía
tamaño 152.639.291 bytes y SHA-256
`58bbdc51669b940a1f08929c8e730805fd1b904c7c1d04cef6a9ca242b610429`.

La ampliación de cabeceras y plantilla incluida se verificó desde fuente el 12 de
octubre de 2026 con 143 pruebas: Domain 40, Application 15, Infrastructure 43,
Console 10 y WPF 35. También se ejecutaron simulación y generación real del
corpus con la plantilla oficial, produciendo 4 clases, 94 alumnos y las 76
incidencias previstas. La plantilla corregida conserva las declaraciones XML de
todos los prefijos indicados por `mc:Ignorable`; el lector rechaza estas
referencias si su espacio de nombres no está declarado. Los cuatro resultados
superaron las comprobaciones ZIP y de compatibilidad XML. No se publicó un
ejecutable nuevo. El entorno sigue sin Word o LibreOffice accesible para
automatizar la inspección visual, que permanece como comprobación manual
pendiente antes de distribuir una nueva entrega.

El pulido final de estados WPF del 12 de octubre de 2026 elevó la suite a 148
pruebas: Domain 40, Application 15, Infrastructure 43, Console 10 y WPF 40. Cubría
la reutilización histórica del plan al volver, resultado inmutable, reinicio de
sesión, cancelación de análisis conservando la plantilla incluida y la frontera
de guardado que mantiene como éxito una publicación completada. La validación
visual/manual exhaustiva de estos estados sigue separada de las pruebas
automatizadas.

El 13 de octubre de 2026 se sustituyó aquella reutilización por el reinicio
completo descrito arriba. La suite quedó en 154 pruebas: Domain 40, Application
15, Infrastructure 43, Console 10 y WPF 46. El build terminó sin avisos y el
formato quedó limpio; no se publicó una entrega nueva.

El rediseño de estados del 13 de octubre de 2026 elevó la suite a 161 pruebas:
Domain 43, Application 15, Infrastructure 45, Console 11 y WPF 47. El formato,
el build sin avisos y la suite completa quedaron limpios. Una simulación de solo
lectura sobre el corpus privado confirmó 22 encontrados, 70 nuevos, 2 conflictos,
92 alumnos incluidos y documentos previstos de 23/22/24/23 alumnos. No se
publicó un ejecutable nuevo ni se generaron documentos en esta verificación.

La entrega WPF se republicó el 12 de octubre de 2026 mediante `dotnet publish`
autocontenido para Windows x64. `release-desktop/DocMerge.exe` conserva versión
`2.1.0.0`/`2.1.0`, tiene tamaño 152.782.139 bytes y SHA-256
`1e68636d1f6953998f9e0b3953e6b1ce61e10cf3b105a8444cbc77d3f3832761`. Una copia
aislada que contenía solo el ejecutable arrancó, materializó la plantilla incluida
con el hash oficial, cerró con código 0 y eliminó su directorio temporal. No
dependió del repositorio, de una plantilla vecina ni de una instalación previa de
.NET.

El rebranding comercial posterior del mismo 12 de octubre publica la WPF como
`release-desktop/Talos.exe`, con versión de archivo `1.0.0.0`, versión de producto
`1.0.0`, ensamblado técnico `2.1.0.0`, tamaño 152.945.961 bytes y SHA-256
`c266edb00eede9d41549ea6fcd2783b00f9f05e2e6c0f7a5b4041844d53f469a`. La
suite completa superó 151 pruebas y una generación real mediante la composición
WPF produjo las cuatro clases previstas sin alterar los 11 archivos de entrada
comprobados. La copia aislada confirmó el icono Windows, la plantilla incrustada
y su limpieza. Se revisaron visualmente claro, oscuro, Sistema, cambio de tema en
caliente, lateral expandido/colapsado y 1366×820; el escalado DPI 125–200 % sigue
requiriendo comprobación manual en equipos configurados con esos factores.

El 14 de octubre de 2026 se implementó desde fuente el panel integrado de detalle
del alumno y el visor enriquecido de medidas, sin modificar el motor de generación.
La suite quedó en 180 pruebas: Domain 44, Application 15, Infrastructure 50,
Console 11 y WPF 60; el build terminó sin avisos y el formato quedó limpio. Una
simulación real confirmó 94 asignaciones, 92 inclusiones, 2 omisiones y los cuatro
documentos previstos, y los 12 archivos de entrada comprobados conservaron sus
hashes. No se publicó una entrega nueva. Permanecen pendientes la validación visual
manual exhaustiva del panel en temas, anchuras y DPI indicados, así como Narrador y
animaciones desactivadas.

La corrección posterior de la jerarquía XAML añade una prueba STA de ventana real:
comprueba padres, límites, ancho de 440 DIP, ausencia de solapamiento, cambio de
alumno, transiciones interrumpidas y recuperación del ancho al cerrar. Se revisaron
capturas con datos ficticios a 1920×1080 y 1366×768, claro/oscuro, modo estrecho y
escalado de layout al 150 %. Este último no sustituye pruebas con DPI nativo de
Windows. La suite pasa a 181 pruebas (WPF 61); no se publica una entrega nueva.
