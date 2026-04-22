# Bluetooth Audio Receiver

Um aplicativo Windows que permite ao seu computador funcionar como um receptor de áudio Bluetooth (A2DP Sink). Com ele, você pode conectar o seu smartphone (ou outro dispositivo compatível) via Bluetooth e escutar músicas, podcasts e outros áudios diretamente nos alto-falantes do seu PC.

## 🎧 Recursos

- **A2DP Sink Nativo**: Utiliza as APIs oficiais do Windows (WinRT) para descobrir e conectar a dispositivos A2DP.
- **Bandeja do Sistema**: O aplicativo pode ser minimizado para rodar de forma invisível no "System Tray" (bandeja do sistema) enquanto o áudio toca.
- **Instalação Descomplicada**: Inclui scripts automatizados (`Instalar.bat` e `Setup-And-Install.ps1`) que cuidam de assinar digitalmente e registrar o aplicativo no Windows como um pacote, garantindo as permissões restritas necessárias para acessar o rádio Bluetooth e rodar áudio em segundo plano.

## ⚠️ Limitações Conhecidas (Windows API)

A API do Windows 10/11 (`AudioPlaybackConnection`) foi projetada apenas para atuar como **Sink (Receptor de Áudio)**. O sistema operacional **não suporta** nativamente o envio de comandos de controle remoto (Play, Pause, Avançar, Retroceder) de volta para o dispositivo móvel via protocolo **AVRCP (Audio/Video Remote Control Profile)**.

Por esse motivo, **você não pode controlar a reprodução pelo computador**. Se você apertar "Play" ou "Pause" no teclado do seu PC, o comando não será repassado ao celular. A reprodução deve ser controlada diretamente pela tela do seu smartphone.

## 💻 Requisitos do Sistema

- **Sistema Operacional**: Windows 10 (Versão 2004, Build 19041) ou mais recente (incluindo Windows 11).
- **Bluetooth**: Um adaptador Bluetooth funcional e ativado no PC.
- **.NET SDK**: SDK do .NET instalado (necessário para a compilação local pelo script de instalação).

## 🚀 Como Instalar e Rodar

Como as conexões de áudio Bluetooth no Windows exigem que o programa rode com capacidades e identidades restritas (Package Identity), o programa não pode ser executado apenas com duplo-clique no `.exe`. Ele precisa ser registrado no Windows. Nós simplificamos isso:

1. Extraia/Clone o repositório em uma pasta de sua preferência.
2. Clique com o botão direito do mouse no arquivo `Instalar.bat` e selecione **"Executar como administrador"**.
3. Aguarde o script do PowerShell terminar. Ele fará o seguinte:
   - Compilará o projeto.
   - Criará um certificado local de desenvolvedor.
   - Instalará o aplicativo no seu sistema (Loose File Registration).
4. Após o script mostrar `SUCESSO`, você pode pesquisar por **"Bluetooth Audio Receiver"** no Menu Iniciar do Windows e abrir o aplicativo.

*(Nota: O aplicativo será instalado em `%LOCALAPPDATA%\BluetoothAudioReceiver`. Não delete essa pasta, pois os arquivos executáveis residem lá).*

## 📱 Como Usar

1. Pareie o seu celular com o seu PC acessando as configurações de Bluetooth do Windows e adicionando um novo dispositivo.
2. Abra o aplicativo **Bluetooth Audio Receiver**.
3. Na janela do aplicativo, selecione o seu dispositivo celular na lista. Se não aparecer, clique em "Atualizar".
4. Clique em **Conectar**.
5. Dê play em uma música no seu celular. O áudio sairá pelos alto-falantes/fones conectados ao seu PC!

## 🛠️ Detalhes Técnicos para Desenvolvedores

O código utiliza:
- **C# / Windows Forms** para a interface gráfica de usuário.
- **`Windows.Media.Audio.AudioPlaybackConnection`**: API do WinRT responsável pela recepção e integração de áudio A2DP.
- **MSIX / AppxManifest**: Manifesto que solicita permissões como `<DeviceCapability Name="bluetooth" />` e `<uap3:Capability Name="backgroundMediaPlayback" />` fundamentais para evitar o erro de acesso negado (`DeniedBySystem`).
