# PC Doctor Pro (`OtimizadorPC.exe`)

Utilitário nativo, leve (~65 KB), código aberto e **100% seguro** para **Windows 10 e Windows 11 (64-bit)** focado em:

- Liberar espaço em disco sem apagar arquivos pessoais, jogos, senhas ou programas;
- Corrigir travamentos e picos de **100% de uso de SSD/disco** (incluindo o bug clássico do `StorAHCI` MSI-Mode e economia de energia SATA AHCI LPM);
- Diagnosticar saúde SMART do disco, erros críticos do Visualizador de Eventos e reparar o Windows (`chkdsk /scan`, `DISM`, `SFC`);
- Criar **Ponto de Restauração do Windows** automaticamente antes de qualquer mudança, com botão dedicado de **Restaurar Padrões**.

---

## Principais Recursos

### Painel em Tempo Real
- Espaço livre e % ocupado no disco do sistema (`C:`)
- Memória RAM livre / total
- Saúde SMART, temperatura e latência máxima do disco do sistema (`C:`)
- Verificação automática de privilégios de Administrador (via manifesto UAC)

### 1. Limpeza Segura de Espaço
- **Temporários, CrashDumps e Logs antigos:** `%TEMP%`, `C:\Windows\Temp`, `CrashDumps`, relatórios WER e logs CBS.
- **Instaladores órfãos:** pastas `*-updater` de aplicativos Electron, `NVIDIA\Downloader` e `$WinREAgent\Scratch`.
- **Caches recriáveis de Dev e Shaders:** `uv`, `bun`, `pip`, `npm`, `yarn`, `pnpm store prune`, `D3DSCache`, NVIDIA `DXCache`/`GLCache`, AMD `DxCache` e Intel `ShaderCache`.
- **Cache do Windows Update & Delivery Optimization:** reinicia serviços com segurança e limpa downloads obsoletos/corrompidos.
- **Opcionais (desmarcados por padrão):** cache de navegadores (Chrome/Brave/Edge) e Spotify *(sem apagar logins, senhas ou favoritos)* e esvaziar Lixeira.

### 2. Anti-SSD 100% & Desempenho
- **TRIM no SSD (`Optimize-Volume -ReTrim`):** recupera a velocidade de escrita e o cache SLC.
- **Desativa economia de energia SATA/AHCI (HIPM/DIPM) e PCIe ASPM:** evita congelamentos na fila de I/O em SSDs SATA.
- **Correção Microsoft `StorAHCI` (MSI-Mode):** desativa `MSISupported` em controladoras AHCI padrão problemáticas que causam picos de 100% de disco.
- **Otimização NTFS:** `disablelastaccess=1`, `disable8dot3=1`, TRIM garantido ativo e `memoryusage=2` (em PCs com >= 12 GB de RAM).
- **Estabilidade:** desativa *Fast Startup* e *Delivery Optimization P2P*, e ajusta o *Shadow Storage* (`Volsnap`) para `4 GB`.

### 3. Diagnóstico & Reparo de Erros
- **Ponto de Restauração antes (Backup):** cria um ponto de restauração nomeado em `C:\` antes de executar qualquer alteração.
- **Auditoria SMART + Event Viewer:** lista erros críticos dos últimos 7 dias (`Kernel-Power`, BSOD, disco, drivers, Windows Update, `Volsnap`).
- **Rede & Drivers:** `ipconfig /flushdns`, expande portas efêmeras TCP/UDP (`10000–65000`), `TcpTimedWaitDelay=30s` e desativa o filtro problemático `VBoxNetLwf` se estiver ativo.
- **Reparo do Sistema:** `chkdsk C: /scan` (online, sem reiniciar), `DISM /StartComponentCleanup`, `SFC /scannow` e opção de `DISM /RestoreHealth`.

### Ferramentas Extras
- **Apenas Diagnosticar:** executa auditoria completa somente leitura sem alterar o sistema.
- **Mapa de Espaço (`C:`):** varredura rápida em passada única (C# nativo) listando as maiores pastas e arquivos (`>= 500 MB`).
- **Restaurar Padrões:** reverte ajustes de NTFS, *Fast Startup*, portas TCP/UDP e `StorAHCI` para o padrão original do Windows.
- **Pasta de Logs:** salva relatórios detalhados com data e hora localmente na pasta `logs\`.

---

## Como Usar

### Opção 1: Baixar pronto (Recomendado)
1. Acesse a aba **[Releases](../../releases)** deste repositório e baixe o arquivo **`OtimizadorPC.exe`**.
2. Dê duplo clique em `OtimizadorPC.exe` e confirme o aviso de Administrador (UAC).
3. Marque as opções desejadas (as opções padrão já são 100% seguras) e clique em **`OTIMIZAR E REPARAR AGORA`**.
4. Ao finalizar, reinicie o computador para aplicar ajustes de driver/NTFS.

### Opção 2: Compilar do código-fonte (Sem instalar SDK!)
O projeto usa apenas o compilador `csc.exe` nativo do `.NET Framework 4.x` já presente em todo Windows 10/11:

```powershell
git clone https://github.com/samuelcaaz/pc-doctor-pro.git
cd pc-doctor-pro
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Isso gera o executável `OtimizadorPC.exe` (~65 KB) na raiz da pasta.

---

## Garantias de Segurança
- **Código 100% aberto e auditável** — sem telemetria, sem conexões externas, sem ofuscação.
- **Nunca** apaga pastas pessoais (`Desktop`, `Downloads`, `Documentos`, `Vídeos`, `Imagens`), jogos, senhas ou programas instalados.
- **Nunca** desativa ou remove Windows Defender, Microsoft Store, Windows Update, Áudio, Rede ou serviços essenciais do sistema.
- Todos os ajustes podem ser revertidos pelo botão **Restaurar Padrões** ou pelo **Ponto de Restauração** criado antes da execução.

## Licença
Distribuído sob a licença [MIT](LICENSE).
