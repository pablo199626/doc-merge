# Contrato de transferencia — fase 1

Estado: fases 1–8 implementadas y verificadas. La transferencia está disponible
en el CLI y mantiene también el modo anterior de ordenación simple.

## Análisis de los archivos locales

Inspección de solo lectura realizada el 7 de octubre de 2026 mediante ZIP/XML
y pypdf 6.19.0, instalado en un directorio temporal de análisis.

- `LISTA_NOMBRES.pdf`: una página, sin cifrado, con texto extraíble. No necesita
  OCR. Contiene título, descripción, curso, responsable, `Alumnado (7)`,
  cabeceras `Nº` / `Nombre y apellidos` y siete entradas numeradas del 1 al 7.
  La extracción separa cada número y su nombre en líneas distintas. El nombre
  de la responsable NO forma parte del alumnado.
- Ambos DOCX contienen dos tablas: una de identificación del grupo y otra de
  personas. Esta última tiene una cabecera y diez filas, con tres celdas por fila.
- La primera columna está vacía de texto; la segunda contiene el nombre y la
  tercera las medidas. Cada persona ocupa un único `w:tr`, con entre 3 y 80
  párrafos contando todas sus celdas. Una fila puede abarcar varias páginas.
- No se detectaron en la tabla objetivo celdas combinadas, tablas anidadas,
  dibujos, hipervínculos, controles de contenido, revisiones, marcadores ni
  referencias a comentarios o notas. Hay una referencia de numeración.
- Las rejillas de ambas tablas tienen anchos 279, 2048 y 13261 DXA.
  Los temas coinciden, pero estilos y numeración difieren. Entre los estilos
  con identificadores compartidos y definiciones distintas están `Normal`,
  `Normal1`, `LO-Normal`, `LO-Normal1`, `LO-normal2` y `Sinespaciado`.
  La definición `abstractNumId=1` también difiere.
- Los siete nombres del PDF tienen coincidencia única en origen y ninguno
  está en destino. Resultado esperado: **7 altas, 0 reemplazos, 10 bajas,
  0 conservaciones y 7 personas finales**, ordenadas con el perfil existente.
  Este ejemplo no prueba reemplazos ni ambigüedades: se necesitan casos ficticios.

## Perfil inicial de entrada

### PDF

Lista numerada de alumnado con texto extraíble y nombres en orden
`Apellidos Nombre`, como en los Word aportados. Buscar la sección y sus
cabeceras; no interpretar todo texto con aspecto de nombre como una persona.
Validar la numeración y contrastar el total declarado con las entradas extraídas
antes de deduplicar. Conservar todas las referencias de entradas repetidas.

La fase 2 incluirá variantes sintéticas de varias páginas y cabeceras repetidas.
Una línea partida solo se unirá si los límites de la entrada son inequívocos.
No inferir personas ni aceptar una extracción parcial silenciosamente. Ausencia
de texto, PDF escaneado, cifrado no accesible o estructura no reconocida son
errores de lectura, nunca una lista vacía válida. OCR queda fuera del perfil
inicial porque el ejemplo no lo requiere. Una lista vacía no autoriza borrados.

### DOCX

Localizar una única tabla con una celda de cabecera cuyo texto normalizado sea
`Nombre y Apellidos`; no elegir simplemente la primera tabla del documento.
La columna de identidad se deriva de esa cabecera (índice 1 en los ejemplos).
El perfil inicial admite tres columnas, cabecera única y una fila por persona.
Una coincidencia en varias tablas es un error estructural, no una ambigüedad
de persona que se pueda ignorar.

Un registro es el **nodo `w:tr` completo**, con sus tres celdas, propiedades,
párrafos, fragmentos de texto y dependencias. No extraer solo el texto de las
medidas ni reconstruir las celdas. Texto vacío o `----` en medidas sigue siendo
información válida del registro y reemplaza al destino si corresponde.

Filas completamente vacías son auxiliares y se conservan. Una fila con datos
pero sin identidad, combinaciones verticales, varias filas por persona o una
estructura no reconocida requieren diagnóstico estructural; no asignar su
contenido a la persona anterior. Recursos adicionales deberán soportarse y
validarse explícitamente antes de admitirlos en la escritura.

## Identidad y procedencia

La clave de comparación usa Unicode NFC, espacios normalizados y `casefold`.
Conservar tildes, `ñ`, guiones y el orden de las palabras. No usar mayúsculas
para separar apellidos, coincidencias parciales, similitud aproximada ni la
función `dividir_nombre` de ordenación para determinar identidad.

Modelo previsto para las siguientes fases:

- Entrada PDF: texto original, clave, página (desde 1), ordinal y localización.
- Registro DOCX: documento de procedencia, índice de tabla y fila (desde 0),
  nombre original, clave, bloque XML y referencias de recursos.
- Acción: tipo, entradas PDF, registros origen/destino e incidencia asociada.
- Plan: acciones, incidencias, conteos y huellas de las tres entradas.

No indexar registros duplicados sobrescribiendo el anterior: cada clave apunta
a una colección. Más de un registro con la misma clave es ambiguo, aunque el
texto o contenido coincidan. Un duplicado del PDF no genera dos filas.

## Decisiones, en orden de prioridad

1. Clave duplicada en destino: conservar TODAS las filas implicadas, incluso
   si no figura en el PDF; registrar la ambigüedad y continuar.
2. Registro único de destino ausente del PDF: eliminarlo de la copia.
3. Persona del PDF con origen único y destino único: reemplazar el bloque
   completo del destino por el de origen, aunque el origen tenga medidas vacías.
4. Persona del PDF con origen único y sin destino: añadir el bloque de origen.
5. Persona del PDF con origen ausente o duplicado y destino único: conservar
   la información de destino; registrar la incidencia y continuar.
6. Persona del PDF sin origen único y sin destino: no crear fila; registrar
   la incidencia y continuar.

Conservar significa mantener contenido y formato; la posición puede cambiar
al ordenar. No mezclar medidas de ambos archivos. Las incidencias de identidad
no detienen la ejecución; los errores de lectura, estructura o integridad sí.

## Formato, orden y salida

El paquete destino aporta el documento base, cabeceras, pies, secciones y
contenido exterior a los registros afectados. Las filas transferidas aportan
el formato del origen. Importar estilos y numeraciones con identificadores
nuevos cuando sea necesario y remapear referencias solo en el contenido
importado. Resolver también dependencias transitivas y formato heredado;
no reemplazar globalmente estilos usados por filas conservadas.

Mantener el perfil `apellido-nombre-segundo-apellido` existente. Su heurística
considera la última palabra nombre y la penúltima segundo apellido; no resuelve
de forma general nombres compuestos. Esta limitación afecta al orden, nunca
a la identidad, y no se cambia en esta fase.

Salida distinta de las tres entradas, incluso con `--force`. Comparar huellas
de entrada y validar el resultado temporal antes de publicarlo. Para ordenar
un único DOCX sigue vigente que solo cambia `word/document.xml`; para transferir
se permitirá únicamente el conjunto de partes y recursos justificado por el plan.

## Casos de aceptación y siguientes entregas

`tests/fixtures/transferencia/casos.json` define casos independientes con nombres
y medidas ficticios, identificadores trazables y acciones esperadas. Su campo
`resultado` expresa contenido y procedencia, no el orden final. `pagina_pdf.txt`
representa la extracción textual del perfil PDF con una responsable señuelo.
Son datos de prueba, no lectores ni documentos binarios ya implementados.

Fase 2 está en `pdf_reader.py`; fase 3, lectura XML, en `docx_processor.py`;
fase 4, planificación, en `matching.py`; fase 5, importación de filas, estilos,
numeración y recursos, en `docx_processor.py`; fase 6, publicación transaccional
y validación, en `transfer.py` y `validation.py`; fases 7–8, CLI y avisos, en
`cli.py` y `matching.py`. Los casos se ejecutan con fixtures ficticios y las
pruebas de integración locales comprueban además los documentos aportados.
