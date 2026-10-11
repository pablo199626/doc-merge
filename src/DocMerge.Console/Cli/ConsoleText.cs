namespace DocMerge.Console.Cli;

internal static class ConsoleText
{
    internal const string Usage = """
        usage: docmerge [-h] [-o OUTPUT] [--destino DESTINATION]
                                 [--lista PDF_LIST] [--simular]
                                 [--orden {apellido-nombre-segundo-apellido,nombre-apellidos,apellidos-coma-nombre}]
                                 [--cabecera HEADER] [--force] [--version]
                                  [source]
               docmerge --generar-clases --historicos DIR --listas DIR
                                  --plantilla TEMPLATE --salida DIR [--simular]
        """;

    internal const string Help = """
        usage: docmerge [-h] [-o OUTPUT] [--destino DESTINATION]
                                 [--lista PDF_LIST] [--simular]
                                 [--orden {apellido-nombre-segundo-apellido,nombre-apellidos,apellidos-coma-nombre}]
                                 [--cabecera HEADER] [--force] [--version]
                                  [source]
               docmerge --generar-clases --historicos DIR --listas DIR
                                  --plantilla TEMPLATE --salida DIR [--simular]

        Ordena tablas Word, transfiere personas o genera documentos de clases.

        positional arguments:
          source                Documento Word (.docx) de entrada

        options:
          -h, --help            show this help message and exit
          -o OUTPUT, --output OUTPUT
                                Ruta del documento ordenado
          --destino DESTINATION
                                DOCX que sirve de base para la transferencia
          --lista PDF_LIST      PDF que determina las personas del resultado
          --simular             Muestra las acciones de transferencia sin generar
                                ningún documento
          --orden {apellido-nombre-segundo-apellido,nombre-apellidos,apellidos-coma-nombre}
                                Regla de ordenación (predeterminada: apellido-nombre-
                                segundo-apellido)
          --cabecera HEADER     Texto que identifica la cabecera de la tabla objetivo
          --force               Sobrescribe la salida existente sin preguntar
          --version             show program's version number and exit
          --generar-clases      Genera un documento por cada clase definida por los PDF
          --historicos DIR      Carpeta raíz de documentos DOCX históricos
          --listas DIR          Carpeta raíz de listados PDF de clases
          --plantilla TEMPLATE  Plantilla DOCX vacía
          --salida DIR          Carpeta nueva donde publicar el lote completo
        """;
}
