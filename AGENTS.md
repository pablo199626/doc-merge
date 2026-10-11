# DocMerge

## Estado del proyecto y referencia histórica

- La migración de la aplicación de consola Python a C# sobre .NET 10 está completada y validada. La implementación C# es la implementación principal y la única que debe evolucionar salvo autorización expresa.
- La implementación Python es un **oráculo histórico de comportamiento** para regresiones y paridad; no es la implementación activa ni debe recibir funcionalidades nuevas.
- Más adelante se añadirá una interfaz gráfica de escritorio para Windows (WPF con MVVM). Por eso el núcleo debe ser independiente de cualquier interfaz: ninguna clase de dominio, aplicación o infraestructura puede usar `Console.*`, `MessageBox` ni tipos de UI.
- `Console.*` solo se permite en el proyecto `DocMerge.Console`. La futura UI WPF consumirá exactamente los mismos casos de uso.
- Prioridad de mantenimiento: conserva los contratos observables y separa las correcciones funcionales de los refactors. No reintroduzcas diferencias frente al comportamiento ya certificado sin una decisión explícita y pruebas de regresión.
- La referencia Python se conserva sin modificar en `legacy/python/`. `legacy/python/reference.sha256` valida sus 27 archivos. Ejecútala desde una copia temporal cuando necesite el documento real local del que depende una prueba. No copies documentos reales dentro de `legacy/` ni los incluyas en Git o en distribuciones.
- Los módulos `identity`, `sorting`, `pdf_reader`, `docx_processor`, `matching`, `validation`, `transfer` y `cli` ya están portados. Todo cambio posterior debe mantener las pruebas de paridad aplicables y pasar la verificación completa.
- El caso de uso **generación de clases** está implementado en C#: descubre una colección de Word históricos y PDF de grupos, construye un índice global de alumnado, genera un DOCX por clase desde una plantilla vacía, valida el lote y lo publica en una carpeta nueva. No forma parte del oráculo Python y debe evolucionar únicamente en C#.
- Si el comportamiento histórico es ambiguo, parece un error o entra en conflicto con el contrato C# ya certificado, pregunta antes de decidir; no lo corrijas en silencio ni lo reinterpretes.

## Estado certificado

- Certificación completada el 8 de octubre de 2026 con .NET SDK `10.0.401`, fijado en `global.json`.
- La suite C# tenía 42 pruebas superadas: Domain 18, Application 1, Infrastructure 16 y Console 7. Estas cifras son una línea base, no un límite; deben aumentar al añadir comportamiento.
- La suite Python archivada tenía 30 pruebas superadas y los 27 hashes de `legacy/python/reference.sha256` coincidían.
- La prueba diferencial del PDF local produjo exactamente las mismas 7 entradas con PdfPig y `pypdf` 6.19.0: total, páginas, texto, identidad y posición.
- En el DOCX local, la ordenación C# coincidió con Python para 10 identidades y cambió exclusivamente `word/document.xml`. La transferencia real produjo las mismas 7 identidades, orden y contenido textual que Python.
- El ejecutable `DocMerge.exe` fue republicado y probado de forma independiente el 9 de octubre de 2026 en `--version`, ordenación, transferencia y asistente sin argumentos. La entrega tiene versión de archivo `2.1.0.0`, versión de producto `2.1.0`, tamaño de 79.658.800 bytes y SHA-256 `c374e28de1d615012ae0db2ba395442537043c2f28bc5ad4a045a7f2481127ff`.
- Los hashes privados de control siguen documentados en `docs/migration-reference.md`; compruébalos antes y después de cualquier prueba con documentos locales.

## Estado actual tras la generación de clases

- El 11 de octubre de 2026 se añadió y validó el flujo de generación de clases, manteniendo intactas las operaciones certificadas de ordenación y transferencia.
- La suite C# actual tiene **89 pruebas superadas**: Domain 40, Application 8, Infrastructure 31 y Console 10.
- El corpus local de la nueva funcionalidad contiene 7 Word históricos (74 registros), 4 PDF de segundo curso (94 asignaciones) y la plantilla `ANEXO_TEMPLATE.docx`. La simulación validada obtiene 22 recuperaciones únicas, 70 ausencias históricas y 2 asignaciones ambiguas del homónimo `BENALI, OMAR` con NIA distintos.
- Los cuatro documentos reales `2A.docx`, `2B.docx`, `2C.docx` y `2D.docx` se generaron y validaron desde el ejecutable publicado. Contienen respectivamente 24, 22, 25 y 23 alumnos.
- La entrega actual `release/DocMerge.exe` conserva versión de archivo `2.1.0.0` y versión de producto `2.1.0`; tiene tamaño de 79.810.864 bytes y SHA-256 `61b42c2d63b791f631d0e6b4e13186a54cde865c9e36e38b862f754df9fcc2d5`.
- El asistente sin argumentos ofrece ahora tres operaciones: transferencia, ordenación y generación de clases. La opción 3 solicita carpeta de históricos, carpeta de PDF, plantilla, carpeta nueva de salida y si se desea simular.
- La arquitectura, los contratos y las instrucciones de mantenimiento del flujo nuevo se documentan en `docs/class-generation.md`.

## Estructura de la solución

```
DocMerge.sln
├── src/
│   ├── DocMerge.Domain          (identidad, ordenación, correspondencias, generación, modelos)
│   ├── DocMerge.Application     (casos de uso, transferencia, generación, validación, interfaces)
│   ├── DocMerge.Infrastructure  (lectura PDF, lectura/escritura DOCX, descubrimiento y publicación)
│   ├── DocMerge.Console         (CLI, asistente de doble clic, composición de dependencias)
│   └── DocMerge.Wpf             (futuro; no crear hasta que se pida)
└── tests/
    ├── DocMerge.Domain.Tests
    ├── DocMerge.Application.Tests
    ├── DocMerge.Infrastructure.Tests
    └── DocMerge.Console.Tests
```

Equivalencia entre módulos Python y su destino en C#:

| Python | Destino en C# |
| --- | --- |
| `cli.py` | `DocMerge.Console` (argumentos e interacción) |
| `sorting.py` | `DocMerge.Domain` (perfiles de ordenación, expuestos con `--orden`) |
| `identity.py` | `DocMerge.Domain` (normalización de identidades exactas) |
| `pdf_reader.py` | `DocMerge.Infrastructure` (extracción y validación del listado) |
| `matching.py` | `DocMerge.Domain` / `DocMerge.Application` (plan de acciones) |
| `transfer.py` | `DocMerge.Application` (operación transaccional) |
| `validation.py` | `DocMerge.Application` (comprobación del resultado) |
| `docx_processor.py` | `DocMerge.Infrastructure` (lectura y modificación del DOCX) |

Las dependencias apuntan hacia el dominio: `Console` → `Application` → `Domain`, e `Infrastructure` → `Application`/`Domain` (implementa sus interfaces). El dominio no referencia ningún otro proyecto ni librerías externas.

## Datos del proyecto

- El nombre público del producto es `DocMerge`, el comando mostrado por el CLI es `docmerge` y el ensamblado debe producir `DocMerge.exe`; los espacios de nombres usan `DocMerge`.
- La versión actual es `2.1.0`. Se define en un único sitio (`Directory.Build.props`) y debe mantenerse sincronizada con las guías y los ejecutables distribuidos. No cambies la versión sin autorización expresa.
- Los contratos externos existentes se conservan exactamente: opciones del CLI (`--destino`, `--lista`, `-o`, `--simular`, `--orden`, `--cabecera`, `--force`), mensajes de usuario, la cabecera exacta `Nombre y Apellidos`, la sección `Alumnado (n)` y los nombres de entrega. La generación añade `--generar-clases`, `--historicos`, `--listas`, `--plantilla` y `--salida`; no reutilices los conceptos origen/destino para este flujo.
- Límite de responsabilidades: la consola procesa argumentos e interacción; la ordenación registra perfiles de forma declarativa y se expone con `--orden`; la identidad normaliza identidades exactas; el lector PDF extrae y valida el listado; las correspondencias construyen el plan de acciones; la transferencia coordina la operación transaccional; la validación comprueba el resultado; el procesador DOCX lee y modifica el documento. Las nuevas reglas de orden van en el módulo de ordenación y no deben modificar el procesador XML.
- En generación, `ClassGenerationService` prepara entradas y plan, `ClassGenerationPlanner` resuelve el índice global, `ClassGenerationExecutionService` coordina generación/validación/publicación, Infrastructure implementa PDF/DOCX/archivos y Console solo solicita datos y presenta `GenerationIssue`. No desplaces estas responsabilidades al asistente ni a la futura WPF.
- El procesador identifica de forma única la tabla por la cabecera exacta `Nombre y Apellidos` (ajustable con `--cabecera`) y deriva de ella la columna de identidad. Mueve o importa nodos XML `<w:tr>` completos: nunca reconstruir un documento ni una tabla, ni leer el contenido a través de texto plano, porque se perderían formato y contenido no tabular.
- La ordenación simple aplica la corrección funcional aprobada tras certificar la paridad: también exige una única cabecera exacta normalizada y usa la columna derivada de esa cabecera. No restaures el comportamiento accidental de Python que aceptaba coincidencias parciales y asumía siempre la segunda columna.
- La ordenación simple solo puede cambiar `word/document.xml`. La transferencia parte de una copia del destino y puede cambiar además estilos, numeración, relaciones, tipos de contenido y recursos estrictamente necesarios para conservar el formato del origen; debe validar el resultado temporal antes de publicarlo.
- La identidad, la interpretación del formato del nombre y la construcción de la clave de ordenación son responsabilidades separadas. Los perfiles iniciales conservan exactamente las heurísticas Python, pero añadir un formato futuro solo debe requerir una estrategia de interpretación, un perfil declarativo y sus pruebas; nunca cambios en el procesador XML.
- La identidad usa coincidencia exacta normalizada: Unicode FormC, colapso de espacios según Python/Unicode 15.0 y full case folding Unicode 15.0 mediante tablas embebidas en `src/DocMerge.Domain/Unicode/`. No sustituirlo por `ToLowerInvariant`, comparadores culturales ni eliminación de tildes o `ñ`. Conserva las pruebas de `ß`, `ẞ`, sigma, `İ`, ligaduras, tildes y espacios Unicode. Las ambigüedades no deben asociar datos dudosos: se conserva el destino cuando exista, se omite la transferencia insegura y se informa al finalizar.
- La generación de clases no modifica esa identidad certificada: usa además `StudentNameMatchKey` como clave tolerante exclusiva del caso de uso. Esta clave ignora comas, caja y acentos, equipara `ü/u`, conserva la diferencia `ñ/n`, colapsa espacios y nunca reordena palabras. Una colisión conserva todos los candidatos y nunca autoriza una selección arbitraria.
- Hay dos perfiles PDF independientes. La transferencia exige texto extraíble y valida la sección numerada `Alumnado (n)`. La generación reconoce listados con `GRUPO` y columnas `Orden`, `NIA`, `Alumno`, `Edad`, `Curso`; contrasta clase de cabecera y nombre de archivo, numeración consecutiva y curso. Los PDF actuales no declaran un total independiente: se permite su validación estructural estricta y se registra `PdfWithoutDeclaredTotal`. Un PDF escaneado, cifrado, vacío o con filas incompletas sigue siendo un error.
- La implementación PDF usa `PdfPig` `0.1.16`, fijado en Infrastructure, tras pruebas diferenciales contra `pypdf` 6.19.0. No cambies biblioteca o versión sin repetir las pruebas diferenciales de ambos perfiles sobre el corpus aprobado.
- La implementación DOCX usa `System.IO.Compression` junto con `System.Xml.Linq`, trabaja sobre elementos `w:tr` completos y copia sin reinterpretar las partes no modificadas. No añadas Open XML SDK ni otra librería DOCX sin una nueva autorización.
- La plantilla de generación debe contener una única tabla identificable por la cabecera exacta, tres columnas compatibles y exclusivamente una fila vacía de prototipo después de la cabecera. La plantilla gobierna la geometría de filas y celdas; el contenido enriquecido y sus dependencias proceden del histórico. El nombre visible procede del PDF.
- Los Word históricos son una base global de información, no origen y destino. Los PDF son la única fuente de pertenencia a las clases. Las búsquedas no se restringen por curso, grupo ni nombre de archivo histórico.
- Un alumno sin histórico, con históricos contradictorios o con identidad ambigua se genera con el nombre del PDF, las demás columnas vacías y sombreado discreto. Estas situaciones se devuelven como avisos estructurados y no detienen el lote; nunca copies información dudosa. Los conflictos estructurales, como clases duplicadas o el mismo NIA asignado varias veces, sí bloquean la publicación.
- La generación extrae e indexa cada documento una sola vez durante la preparación; las relecturas posteriores se limitan a cargar paquetes para generar y a verificar hashes de integridad. Genera en una carpeta temporal hermana, valida cada DOCX y publica el directorio completo mediante movimiento atómico. La carpeta final debe ser nueva; `--force` no está permitido para este flujo.
- Verificación principal: `dotnet test`. La suite debe cubrir ordenación, ambos perfiles PDF, lectura DOCX, claves exacta y tolerante, correspondencias, transferencia, indexación histórica, generación multiorigen, formato/recursos, validación, publicación transaccional, CLI y las tres opciones del asistente, conservando además la paridad Python aplicable. Los documentos locales aportados se usan solo para lectura y nunca deben modificarse.
- Ejecutar desde fuente: `dotnet run --project src/DocMerge.Console -- "assets/documents/PAABLO ANEXOS.docx" --force`.
- Transferir por listado: `dotnet run --project src/DocMerge.Console -- ORIGEN.docx --destino DESTINO.docx --lista LISTA.pdf -o RESULTADO.docx`. Añadir `--simular` para revisar el plan sin generar salida.
- Generar clases: `dotnet run --project src/DocMerge.Console -- --generar-clases --historicos HISTORICOS --listas PDF --plantilla PLANTILLA.docx --salida CARPETA_NUEVA`. Añadir `--simular` para obtener plan y avisos sin generar salida. El perfil predeterminado para este flujo es `apellidos-coma-nombre`.
- Sin argumentos, el programa y el `.exe` abierto con doble clic muestran un asistente interactivo con las tres operaciones. Debe admitir rutas y carpetas arrastradas con comillas y mantener la ventana abierta al terminar.
- La plataforma soportada es Windows x64 sobre .NET 10 LTS. Para crear el ejecutable: `dotnet publish src/DocMerge.Console -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`. No publiques x86 hasta que exista una necesidad y una matriz de pruebas aprobadas. El resultado debe llamarse `DocMerge.exe`.
- La entrega actual está en `release\` y contiene exclusivamente `DocMerge.exe` y `REEDME.txt`. El ZIP anterior permanece retirado: no recrees `DocMerge.zip` salvo petición expresa. La distribución pública no debe incluir documentos reales, datos personales, rutas locales, PDB ni archivos de prueba.
- Los documentos locales privados están en `assets\documents\` y los resultados generados que se decida conservar, en `assets\outputs\`. Ambas carpetas están ignoradas por Git. No muevas estos archivos a `src\`, `tests\`, `legacy\` ni `release\`.
- La inyección de dependencias usa `Microsoft.Extensions.DependencyInjection` `10.0.12`, fijado en el proyecto Console. Propón y autoriza cualquier actualización o dependencia nueva antes de incorporarla.
- En este equipo, `dotnet` puede resolver primero la instalación x86 sin SDK. Si ocurre, ejecuta `C:\Program Files\dotnet\dotnet.exe`; no cambies el target ni instales otra versión como solución accidental.
- Los artefactos de compilación (`bin/`, `obj/`, `publish/`, `dist/`) y las cachés no son código fuente y deben estar en `.gitignore`.

# Reglas de programación

## Aplicación al proyecto

- Aplica estas reglas a todo el código C# nuevo. El código Python archivado en `legacy/python/` solo se lee como referencia y no se modifica.
- Los contratos de documentos, opciones públicas del CLI y nombres de entrega descritos arriba deben conservar su compatibilidad. Los identificadores en C# van siempre en inglés, aunque la referencia Python tenga nombres en español: usa equivalentes naturales en inglés y mantén el glosario de `docs/glossary.md`.
- Los textos para el usuario y los valores externos, como `Nombre y Apellidos` o `Alumnado (n)`, pueden estar en español: son contenido o contratos de entrada, no identificadores internos.

## Control de flujo

- Evita `else` y `else if` salvo que sean realmente necesarios. Prefiere:
  - Retornos tempranos (guard clauses): valida errores y casos límite al inicio.
  - Diccionarios o polimorfismo frente a cadenas largas de condiciones.
  - Expresiones `switch` y pattern matching solo cuando haya varios casos discretos sobre un mismo valor.
  - Métodos con nombres descriptivos para expresar condiciones complejas.
- Máximo dos niveles de anidación dentro de un método. Extrae métodos o usa retornos tempranos si se supera ese límite.
- Evita condiciones negativas dobles; usa nombres en positivo.
- Evita números y cadenas mágicas con significado de negocio: usa constantes (`const`, `static readonly`) o enums con nombre.

## Funciones y clases

- Un método hace una sola cosa y su nombre dice qué hace.
- Mantén métodos cortos, idealmente de menos de 20–30 líneas, y con un máximo de 3–4 parámetros. Agrupa parámetros relacionados en un `record` u objeto de opciones cuando sean más.
- Evita parámetros booleanos que cambian el comportamiento (flag arguments); crea métodos separados con nombres explícitos.
- Prefiere métodos puros y evita efectos secundarios ocultos. Haz explícitas las operaciones de lectura, escritura y publicación de archivos.
- Mantén clases pequeñas y cohesionadas. Prefiere composición sobre herencia. Marca las clases como `sealed` salvo que se diseñen para herencia.

## Principios de diseño

- Aplica SOLID, especialmente responsabilidad única e inversión de dependencias.
- DRY: no dupliques lógica, pero evita abstraer prematuramente; usa la regla de tres para identificar patrones reales.
- KISS y YAGNI: elige la solución más simple que funcione y no implementes funcionalidades que no se han pedido.
- Respeta la ley de Demeter: un objeto no debe conocer los detalles internos de otros.
- Prefiere inmutabilidad y no mutes parámetros de entrada. Usa `record`, `readonly`, `init`, `IReadOnlyList<T>`/`IReadOnlyDictionary<TKey, TValue>` y colecciones inmutables cuando proceda.
- Para modificar XML, trabaja sobre copias de los nodos y paquetes cuando corresponda; conserva las garantías de integridad indicadas en las reglas del proyecto.
- Habilita `<Nullable>enable</Nullable>` y trata los avisos como errores (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`) en todos los proyectos.

## Arquitectura

- Separa responsabilidades de dominio, aplicación, infraestructura y presentación según la estructura de solución indicada; no crees proyectos, capas o carpetas sin una necesidad concreta.
- El dominio no depende de frameworks, bases de datos ni librerías externas. Las dependencias apuntan hacia el dominio, nunca al revés.
- Mantén las reglas de identidad, ordenación y correspondencias separadas de la interacción del CLI y de la lectura/escritura DOCX/PDF.
- El núcleo no conoce la interfaz: la entrada y la salida se abstraen con interfaces o se devuelven como resultados (planes, informes, errores). Los casos de uso reciben datos y devuelven resultados; no imprimen ni leen del usuario.
- Los avisos y los resultados de la operación (por ejemplo, ambigüedades omitidas) se devuelven como datos estructurados; la capa de presentación decide cómo mostrarlos. Esto permitirá reutilizarlos en la futura interfaz gráfica.
- Las operaciones de E/S de archivos y las largas usan `async/await` con `CancellationToken`, para no bloquear una ventana en el futuro. No uses `.Result` ni `.Wait()`.
- Usa inyección de dependencias explícita con `Microsoft.Extensions.DependencyInjection`, compuesta únicamente en el proyecto de entrada (`DocMerge.Console`). Evita estado global mutable y singletons ocultos. Las constantes y los registros declarativos de perfiles no deben convertirse en estado mutable de ejecución.
- El parser del CLI es propio y acotado: conserva las opciones y aliases completos documentados, `--opcion=valor`, `--` y la semántica observable cubierta por pruebas. No es obligatorio reproducir abreviaciones accidentales de `argparse`. Los mensajes controlados por la aplicación son contratos exactos; los detalles procedentes del sistema operativo o de bibliotecas se comparan por tipo y datos estructurados.
- Accede a datos externos, como PDF, DOCX o el sistema de archivos, mediante interfaces definidas en Application o Domain e implementadas en Infrastructure; no añadas infraestructura hipotética.
- Valida y transforma los datos en los límites del sistema. El interior trabaja con datos normalizados y mantiene sus invariantes, sin repetir conversiones de entrada.

## Nombres y legibilidad

- Usa nombres descriptivos y pronunciables, sin abreviaturas crípticas.
- Nombra métodos con verbos y clases y variables con sustantivos; aplica las reglas de inglés de la sección de naming.
- El código debe explicarse por sí mismo. Los comentarios explican el porqué, las restricciones o las decisiones, no repiten lo que hace el código. Usa comentarios XML (`///`) en la API pública cuando aporten información que el nombre no da.
- No dejes código muerto ni código comentado. Todo `TODO` debe incluir contexto y una condición de resolución o referencia a una tarea.
- Respeta las convenciones y el estilo definidos en `.editorconfig`.

## Errores y robustez

- Maneja los errores explícitamente; nunca silencies excepciones con un `catch` vacío. Si un error es recuperable, documenta el motivo y ejecuta una recuperación concreta.
- Falla rápido: lanza o retorna el error en cuanto lo detectes.
- Usa excepciones específicas (derivadas de una excepción base del proyecto) con mensajes útiles y conserva la causa al traducir excepciones entre capas pasándola como `innerException`. No uses `throw ex;`; usa `throw;` para relanzar. Evita devolver `null` para indicar un fallo; resérvalo para ausencias válidas y documentadas.
- No registres ni expongas contraseñas, tokens u otros datos sensibles. Los diagnósticos deben incluir solo la información necesaria; las pruebas y ejemplos compartidos usan datos ficticios.
- Nunca pongas secretos en el código; usa variables de entorno o `dotnet user-secrets`.
- Mantén la escritura transaccional: valida temporales antes de publicar y limpia los recursos incluso si ocurre un error (`using`/`await using`, `try/finally`). Publica el resultado de forma atómica (mover el temporal al destino final una vez validado).
- Libera siempre los flujos, paquetes y documentos con `using`/`await using`.

## Tests

- Toda lógica nueva lleva tests de comportamiento; no rompas los existentes. Las correcciones de errores deben incluir una prueba de regresión cuando corresponda.
- Usa xUnit. Los tests son independientes, rápidos y deterministas. Usa directorios temporales y fixtures ficticios; nunca dependas del orden de ejecución.
- Conserva las **pruebas de paridad** de cada módulo: los mismos casos de entrada/salida que la suite Python equivalente, con fixtures ficticios. Los documentos reales aportados se usan solo para lectura y nunca deben modificarse.
- Las incidencias de destinos ambiguos se ordenan de forma determinista según la primera aparición en el documento de destino. Esta es una diferencia aprobada frente al orden accidental de un `set` de Python.
- Estructura las pruebas como Arrange / Act / Assert y verifica un concepto por test.
- Nombra los tests en inglés por el comportamiento que verifican, con el patrón `Method_Scenario_ExpectedResult` (por ejemplo, `Normalize_NameWithAccents_KeepsAccents`).
- Comprueba resultados observables y garantías relevantes, como integridad de entradas y publicación atómica; evita pruebas que solo repliquen la implementación.

## Forma de trabajar

- Antes de crear algo nuevo, busca si ya existe en el proyecto.
- Haz cambios pequeños y enfocados; no refactorices lo que no se pidió.
- Ejecuta formato, tests y build aplicables antes de dar una tarea por terminada: `dotnet format --verify-no-changes`, `dotnet build -warnaserror` y `dotnet test`. Comunica cualquier comprobación que no se haya podido ejecutar.
- Para entregar un ejecutable, ejecuta también `dotnet publish` como se indica arriba, verifica su versión y prueba el flujo afectado (incluido el asistente de doble clic) antes de distribuirlo.
- Para cambios exclusivamente documentales, revisa el contenido y ejecuta `git diff --check`; no es necesario recompilar el ejecutable.
- Si falta una configuración de formato o analizadores, indícalo en la verificación cuando sea relevante; no introduzcas nuevas herramientas o paquetes NuGet como efecto secundario de otra tarea. Toda dependencia nueva se propone antes al usuario.
- Si algo es ambiguo y afecta al comportamiento, los contratos o el alcance, pregunta antes de implementarlo.

## Naming (siempre en inglés)

- Todos los identificadores van en inglés: clases, interfaces, variables, métodos, propiedades, constantes, enums, parámetros, proyectos, espacios de nombres, archivos y carpetas.
- Esto incluye nombres de tablas, columnas, endpoints, claves de configuración y eventos que definamos internamente. Respeta los contratos externos descritos en «Aplicación al proyecto».
- No mezcles idiomas en un mismo nombre: `GetUsuarioById` es incorrecto; `GetUserById` es correcto.
- No traduzcas literalmente términos del negocio: usa el equivalente natural en inglés y mantén un glosario (`docs/glossary.md`) si hay dudas. Por ejemplo, el módulo `REGLAS_ORDENACION` pasa a un registro de `SortingRules`/`SortingProfiles`.
- Usa verbos en métodos (`CalculateTotal`, `FindUserById`), sustantivos en clases y variables (`OrderService`, `totalPrice`) y prefijos `Is`, `Has` o `Can` en booleanos (`IsActive`, `HasPermission`, `CanTransfer`).
- Respeta el casing de C#: `PascalCase` en tipos, métodos, propiedades, enums, espacios de nombres y constantes; `camelCase` en parámetros y variables locales; `_camelCase` en campos privados; prefijo `I` en interfaces (`IDocumentReader`); sufijo `Async` en métodos asíncronos. Conserva `docmerge` como nombre del comando, `DocMerge.exe` como ejecutable y `DocMerge` como espacio de nombres raíz.
- Evita abreviaturas crípticas y nombres de una letra, salvo contadores en bucles cortos.
