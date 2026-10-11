using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Cogana.Admin.Escritorio.Servicios;

public sealed record CuentaRecordada(string Correo, string Contrasena)
{
    public override string ToString() => Correo;
}

public interface ICuentaRecordada
{
    CuentaRecordada? Leer();
    void Guardar(string correo, string contrasena);
    void Olvidar();
}

/// <summary>Credencial genérica de Windows, limitada a este usuario, equipo y proyecto.</summary>
public sealed class CuentaRecordadaWindows(string destino) : ICuentaRecordada
{
    private const uint TipoGenerico = 1;
    private const uint PersistenciaLocal = 2;
    private const int NoEncontrada = 1168;

    public CuentaRecordada? Leer()
    {
        if (!CredRead(destino, TipoGenerico, 0, out var puntero))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == NoEncontrada) return null;
            throw new Win32Exception(error);
        }
        try
        {
            var credencial = Marshal.PtrToStructure<Credencial>(puntero);
            if (credencial.Tamano == 0 || credencial.Tamano > 2560 || credencial.Tamano % 2 != 0 ||
                credencial.Secreto == IntPtr.Zero || string.IsNullOrWhiteSpace(credencial.Usuario)) return null;
            var secreto = Marshal.PtrToStringUni(credencial.Secreto, (int)credencial.Tamano / 2);
            return secreto is null ? null : new(credencial.Usuario, secreto);
        }
        finally { CredFree(puntero); }
    }

    public void Guardar(string correo, string contrasena)
    {
        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrEmpty(contrasena) || contrasena.Length > 1280)
            throw new ArgumentException("La cuenta no es válida para recordar.");
        var puntero = Marshal.StringToCoTaskMemUni(contrasena);
        try
        {
            var credencial = new Credencial
            {
                Tipo = TipoGenerico, Destino = destino, Usuario = correo.Trim(),
                Comentario = "Cuenta recordada de Cogana Admin", Persistencia = PersistenciaLocal,
                Tamano = (uint)contrasena.Length * 2, Secreto = puntero
            };
            if (!CredWrite(ref credencial, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.ZeroFreeCoTaskMemUnicode(puntero); }
    }

    public void Olvidar()
    {
        if (!CredDelete(destino, TipoGenerico, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != NoEncontrada) throw new Win32Exception(error);
        }
    }

    // Contrato oficial: https://learn.microsoft.com/windows/win32/api/wincred/ns-wincred-credentialw
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credencial
    {
        public uint Flags, Tipo;
        public string? Destino, Comentario;
        public System.Runtime.InteropServices.ComTypes.FILETIME Fecha;
        public uint Tamano;
        public IntPtr Secreto;
        public uint Persistencia, CantidadAtributos;
        public IntPtr Atributos;
        public string? Alias, Usuario;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string destino, uint tipo, uint flags, out IntPtr credencial);
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credencial credencial, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string destino, uint tipo, uint flags);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr credencial);
}
