# Regenera el recurso ps_2_0 con el compilador incluido en Windows. No se ejecuta al iniciar la app.
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
public static class CompiladorPerspectiva {
    [ComImport, Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IBlob { [PreserveSig] IntPtr GetBufferPointer(); [PreserveSig] UIntPtr GetBufferSize(); }
    [DllImport("d3dcompiler_47.dll", CharSet=CharSet.Ansi)]
    private static extern int D3DCompile(byte[] source, UIntPtr length, string name, IntPtr defines, IntPtr include,
        string entry, string target, uint flags, uint flags2, out IBlob code, out IBlob errors);
    public static void Compilar(string origen, string destino) {
        byte[] fuente = File.ReadAllBytes(origen);
        IBlob codigo, errores;
        int resultado = D3DCompile(fuente, (UIntPtr)fuente.Length, "PerspectivaTarjeta.hlsl", IntPtr.Zero, IntPtr.Zero,
            "main", "ps_2_0", 0x8000, 0, out codigo, out errores);
        try {
            if(resultado < 0) throw new Exception(errores == null ? "No se pudo compilar la perspectiva." : Marshal.PtrToStringAnsi(errores.GetBufferPointer()));
            byte[] datos = new byte[(int)codigo.GetBufferSize()];
            Marshal.Copy(codigo.GetBufferPointer(), datos, 0, datos.Length);
            File.WriteAllBytes(destino, datos);
        } finally {
            if(codigo != null) Marshal.ReleaseComObject(codigo);
            if(errores != null) Marshal.ReleaseComObject(errores);
        }
    }
}
'@
[CompiladorPerspectiva]::Compilar((Join-Path $PSScriptRoot 'PerspectivaTarjeta.hlsl'), (Join-Path $PSScriptRoot 'PerspectivaTarjeta.ps'))
Write-Output 'Perspectiva compilada como ps_2_0.'
