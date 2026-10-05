"""Configurable ordering rules for rows in a people table."""


def dividir_nombre(nombre_completo):
    """Split ``Apellido1 Apellido2 Nombre`` into its sortable parts."""
    partes = nombre_completo.split()
    if len(partes) < 3:
        return None
    return {
        "apellido_1": " ".join(partes[:-2]),
        "apellido_2": partes[-2],
        "nombre": partes[-1],
    }


def clave_apellido_nombre_segundo_apellido(nombre_completo):
    """Sort by first surname, given name, then second surname."""
    partes = dividir_nombre(nombre_completo)
    if partes is None:
        return ("\uffff", "\uffff", "\uffff")
    return (
        partes["apellido_1"].casefold(),
        partes["nombre"].casefold(),
        partes["apellido_2"].casefold(),
    )


def clave_nombre_apellidos(nombre_completo):
    """Sort by given name, then first and second surname."""
    partes = dividir_nombre(nombre_completo)
    if partes is None:
        return ("\uffff", "\uffff", "\uffff")
    return (
        partes["nombre"].casefold(),
        partes["apellido_1"].casefold(),
        partes["apellido_2"].casefold(),
    )


REGLAS_ORDENACION = {
    "apellido-nombre-segundo-apellido": clave_apellido_nombre_segundo_apellido,
    "nombre-apellidos": clave_nombre_apellidos,
}


def obtener_regla(nombre_regla):
    """Return a configured sort-key function or raise an actionable error."""
    try:
        return REGLAS_ORDENACION[nombre_regla]
    except KeyError as error:
        disponibles = ", ".join(REGLAS_ORDENACION)
        raise ValueError(f"Regla de ordenación no válida: {nombre_regla}. Disponibles: {disponibles}") from error
