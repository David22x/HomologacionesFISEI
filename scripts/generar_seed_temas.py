"""Genera seed_temas_sprint2.json a partir de los planes analiticos (PDF) de TI y SW.

Uso:
    pip install pdfplumber
    python generar_seed_temas.py <carpeta_con_planes_pdf> [<otra_carpeta> ...] -o seed_temas_sprint2.json

Regla de conversion (hoja "III CONTENIDO" del plan analitico):
    Tema    = "Unidades tematicas"  (una fila de la tabla)
    Subtema = "Contenidos minimos"  (texto de esa misma fila, limpio)
La asignatura se identifica por el codigo UTA-FISEI-... del plan, igual al del catalogo.
"""
import argparse, glob, json, os, re
import pdfplumber

FUENTE = "80 planes analiticos jul-dic 2026 (40 TI y 40 SW)"


def limpiar(s):
    s = (s or "").replace("ï¿½", "ó")  # caracter danado en un plan: siempre era una "o" con tilde
    s = re.sub(r"^[•·\-\s]+", "", s)
    return re.sub(r"\s+", " ", s).strip()


def extraer(ruta):
    with pdfplumber.open(ruta) as pdf:
        texto = "\n".join((p.extract_text() or "") for p in pdf.pages[:3])
        m = re.search(r"Código\s+(UTA-[A-Z0-9\-]+)", texto)
        codigo = m.group(1) if m else None
        m = re.search(r"Nombre de la asignatura\s*\n(.+)", texto)
        nombre = limpiar(m.group(1)) if m else None
        filas = []
        unidad = None
        for pg in pdf.pages:
            for tb in pg.extract_tables():
                if not tb or not any("Unidades tem" in (c or "") for c in tb[0]):
                    continue
                for fila in tb[1:]:
                    u, tema, contenidos = ((fila + [None] * 3)[:3])
                    if (u or "").startswith(("Resultados", "Total")):
                        continue
                    if u:
                        unidad = limpiar(u)
                    if limpiar(tema):
                        filas.append((unidad, limpiar(tema), limpiar(contenidos)))
    return codigo, nombre, filas


def construir(codigo, nombre, filas):
    vistos, temas = {}, []
    for unidad, tema, contenidos in filas:
        clave = tema.lower()
        vistos[clave] = vistos.get(clave, 0) + 1
        nombre_tema = tema if vistos[clave] == 1 else f"{tema} ({unidad})"
        subtemas = [{"orden": 1, "nombre": contenidos}] if contenidos else []
        temas.append({"orden": len(temas) + 1, "nombre": nombre_tema[:300], "subtemas": subtemas})
    return {"codigo": codigo, "nombre": nombre, "temas": temas}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("carpetas", nargs="+")
    ap.add_argument("-o", "--salida", default="seed_temas_sprint2.json")
    a = ap.parse_args()
    asignaturas, problemas = [], []
    for carpeta in a.carpetas:
        for ruta in sorted(glob.glob(os.path.join(carpeta, "**", "*.pdf"), recursive=True)):
            codigo, nombre, filas = extraer(ruta)
            if not codigo or not filas:
                problemas.append(os.path.basename(ruta))
                continue
            asignaturas.append(construir(codigo, nombre, filas))
    asignaturas.sort(key=lambda x: x["codigo"])
    json.dump({"version": 1, "fuente": FUENTE, "asignaturas": asignaturas},
              open(a.salida, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    temas = sum(len(x["temas"]) for x in asignaturas)
    print(f"{len(asignaturas)} asignaturas, {temas} temas -> {a.salida}")
    if problemas:
        print("REVISAR (sin codigo o sin tabla de contenidos):", *problemas, sep="\n  ")


if __name__ == "__main__":
    main()
