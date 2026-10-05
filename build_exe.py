"""Build the Windows executable: ``python build_exe.py``."""

from PyInstaller.__main__ import run


run([
    "--noconfirm",
    "--clean",
    "--onefile",
    "--console",
    "--name=document-operator",
    "document_operator/__main__.py",
])
