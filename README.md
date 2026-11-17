# G5 Link

Aplicativo independente para Windows que configura por Bluetooth LE o **Machenike G5Pro Max da versão com base de carregamento**. Os testes foram realizados especificamente nessa versão; a compatibilidade com outras versões ou kits não foi confirmada. Projeto experimental, sem vínculo com a Machenike ou com o KeyLinker.

## O que faz

- Procura o controle ao iniciar, conecta automaticamente quando encontra apenas um e lê as configurações.
- Ajusta iluminação RGB, brilho e respiração; remapeamento; zonas mortas, curvas e inversão dos analógicos; gatilhos, vibração e turbo.
- Lê e preserva macros; permite editar slots vazios ou em formato digital reconhecido.
- Cria backups locais e permite importar backups compatíveis de iluminação, botões, analógicos, gatilhos, vibração e turbo. Alterações são enviadas apenas ao clicar em **Aplicar**; a gravação é conferida por releitura.
- Exibe teste ao vivo de comandos recebidos pelo Windows e uma vista superior esquemática de LB, LT, RB e RT.

## Requisitos e execução

Windows 10 versão 2004 ou posterior, Windows 11, Bluetooth LE e um G5Pro Max da versão com base de carregamento. A comunicação testada é diretamente com o controle via Bluetooth; não é necessário conectar a base para usá-la como interface de configuração. Para compilar, instale o **SDK do .NET 8** e execute, na raiz do repositório:

```powershell
dotnet publish windows/G5Link.csproj -c Release -r win-x64 --self-contained true -o dist
.\dist\G5Link.exe
```

A publicação acima inclui o runtime para Windows x64. Mantenha todos os arquivos da pasta `dist` juntos e use uma pasta com permissão de escrita para os backups e o registro local. Não existe instalador nem release pronta neste repositório.

Ligue o controle no modo Bluetooth utilizado pelo KeyLinker. O G5 Link busca por 15 segundos; se encontrar mais de um, selecione o controle e clique em **Conectar**. Se uma leitura falhar, o status indica leitura parcial; reconecte para tentar novamente. Para alterar uma configuração, revise os valores na aba correspondente e clique em **Aplicar**. **Restaurar original** utiliza os valores lidos na conexão atual, conforme a área.

## Estado e limitações

Testado com um G5Pro Max via Bluetooth: descoberta, leitura e testes de gravação com releitura e restauração em iluminação, mapeamento, zonas mortas, gatilhos, vibração, turbo e um slot de macro. A cor da iluminação decorativa foi confirmada visualmente. A resposta de todas as curvas, motores e macros em jogos ainda não foi validada.

- USB e receptor de 2,4 GHz não foram implementados ou validados para configuração.
- Não foi identificado comando para mudar as cores dos LEDs centrais ou do botão power.
- Power/FN e botões traseiros M1/M2 não foram identificados como entradas físicas no perfil Windows testado. O teste ao vivo mostra comandos reportados pelo Windows, possivelmente após remapeamento; com mais de um gamepad, a visualização é pausada. Um único gamepad detectado não garante identificação física exclusiva.
- Macros de formato desconhecido são preservadas e bloqueadas para edição. Movimentos analógicos em macros e importação de backups de macros não estão implementados.
- A imagem e as prévias de iluminação são ilustrativas. Alguns perfis e modelos podem se comportar de forma diferente.

## Privacidade e backups

Backups e `g5link.log` são gravados ao lado do executável; o registro pode conter identificadores e detalhes do dispositivo. Esses arquivos, APKs, firmware e dados extraídos não fazem parte deste repositório. Não publique backups ou logs sem revisá-los.

## Estrutura e contribuições

O código está em [`windows/`](windows/). O protocolo foi estudado a partir do comportamento do KeyLinker e das comunicações com o dispositivo. São bem-vindos relatos reproduzíveis com modelo, modo de conexão e versão do Windows, sem identificadores pessoais.

A fotografia da visualização tem origem na [página do produto da iGeek](https://igeekjo.com/collections/gaming-controller/products/machenike-g5pro-max-1000hz-wireless-gaming-controller-white) e é material de terceiro; sua presença aqui não concede licença sobre a imagem. O nome e as marcas pertencem aos respectivos titulares. Nenhuma licença de código foi definida neste repositório.
