#!/usr/bin/env python3
"""
Lint de XAML para NABeauty.App — detecta antes de compilar los errores que ya costaron builds o bugs visuales.

  python tests/xaml_lint.py

Reglas:
  1. XML bien formado.
  2. Todo {StaticResource X} / {DynamicResource X} existe (App.xaml, temas, o x:Key en el mismo archivo).
  3. Ningún token de TEMA se usa con StaticResource (no cambiaría al alternar claro/oscuro).
  4. Claro.xaml y Oscuro.xaml tienen exactamente las mismas claves.
  5. En un <Style>, ningún <Setter> después de <Style.Triggers>/<Style.Resources> (error MC3088).
  6. x:Class coincide con la clase del .xaml.cs, y cada manejador de evento (Click="X") existe en el código.
  7. Todo archivo pack://application:,,,/... referenciado existe en el proyecto.
  8. Toda plantilla de botón usa Margin="{TemplateBinding Padding}" en su ContentPresenter.
"""
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET

RAIZ = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "NABeauty.App")
NS_X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
errores: list[str] = []


def err(archivo: str, msg: str) -> None:
    errores.append(f"{os.path.relpath(archivo, RAIZ)}: {msg}")


def claves(arbol: ET.Element) -> set[str]:
    return {e.get(NS_X + "Key") for e in arbol.iter() if e.get(NS_X + "Key")}


archivos = sorted(glob.glob(os.path.join(RAIZ, "**", "*.xaml"), recursive=True))
archivos = [a for a in archivos if f"{os.sep}obj{os.sep}" not in a and f"{os.sep}bin{os.sep}" not in a]

arboles: dict[str, ET.Element] = {}
for a in archivos:
    try:
        arboles[a] = ET.parse(a).getroot()
    except ET.ParseError as e:
        err(a, f"XML mal formado: {e}")

# ── Recursos globales ──
claro = arboles.get(os.path.join(RAIZ, "Themes", "Claro.xaml"))
oscuro = arboles.get(os.path.join(RAIZ, "Themes", "Oscuro.xaml"))
app = arboles.get(os.path.join(RAIZ, "App.xaml"))
claves_tema = claves(claro) if claro is not None else set()
if claro is not None and oscuro is not None and claves(claro) != claves(oscuro):
    err("Themes", f"claves distintas entre temas: {sorted(claves(claro) ^ claves(oscuro))}")
claves_globales = claves_tema | (claves(app) if app is not None else set())

PATRON_REC = re.compile(r"\{(Static|Dynamic)Resource\s+([^}\s]+)\}")
PATRON_EVENTO = re.compile(r'\s(Click|KeyDown|KeyUp|Loaded|Checked|Unchecked|SelectionChanged|PasswordChanged|TextChanged|MouseDoubleClick|ClaveCambiada)="([A-Za-z_]\w*)"')

for a, raiz in arboles.items():
    texto = open(a, encoding="utf-8").read()
    es_tema = os.sep + "Themes" + os.sep in a
    locales = claves(raiz)

    # 2 y 3: recursos
    for tipo, clave in PATRON_REC.findall(texto):
        if clave.startswith("{"):
            continue  # {x:Type …} y similares
        if clave not in claves_globales and clave not in locales:
            err(a, f"recurso inexistente: {tipo}Resource {clave}")
        if tipo == "Static" and clave in claves_tema and not es_tema:
            err(a, f"token de tema con StaticResource (no cambiaría de tema): {clave}")

    # 5: orden en Style
    for estilo in raiz.iter():
        if not estilo.tag.endswith("}Style") and estilo.tag != "Style":
            continue
        visto_prop = False
        for hijo in list(estilo):
            nombre = hijo.tag.split("}")[-1]
            if nombre in ("Style.Triggers", "Style.Resources"):
                visto_prop = True
            elif nombre == "Setter" and visto_prop:
                err(a, "Setter después de Style.Triggers/Style.Resources (MC3088)")

    # 8: plantillas de botón
    for plantilla in raiz.iter():
        if plantilla.tag.endswith("ControlTemplate") and plantilla.get("TargetType") in ("Button", "{x:Type Button}"):
            for cp in plantilla.iter():
                if cp.tag.endswith("ContentPresenter") and cp.get("Margin") != "{TemplateBinding Padding}":
                    err(a, "ContentPresenter de botón sin Margin=\"{TemplateBinding Padding}\"")

    # 7: archivos pack://
    for ruta in re.findall(r"pack://application:,,,/([^\"#}]+?)(?:[\"#}])", texto):
        if not os.path.exists(os.path.join(RAIZ, ruta.replace("/", os.sep))):
            err(a, f"archivo inexistente: {ruta}")

    # 6: code-behind
    clase = raiz.get(NS_X + "Class")
    if clase and not es_tema:
        cs = a + ".cs"
        if not os.path.exists(cs):
            err(a, "falta el .xaml.cs")
            continue
        codigo = open(cs, encoding="utf-8").read()
        nombre = clase.split(".")[-1]
        if not re.search(rf"partial class {nombre}\b", codigo):
            err(a, f"x:Class {clase} no coincide con el .xaml.cs")
        for _, metodo in PATRON_EVENTO.findall(texto):
            if not re.search(rf"void {metodo}\s*\(", codigo):
                err(a, f"manejador de evento inexistente: {metodo}")

if errores:
    print("\n".join(errores))
    print(f"\n{len(errores)} problema(s) en {len(arboles)} archivos XAML")
    sys.exit(1)
print(f"XAML OK: {len(arboles)} archivos, {len(claves_tema)} tokens por tema, 0 problemas")
