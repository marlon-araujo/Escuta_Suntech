using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using Projeto_Classes.Classes;
using Projeto_Classes.Classes.Gerencial;
using System.Globalization;
using System.Data;
using System.Collections;
using System.Xml;


namespace Monitoramento
{
    class Program
    {
        private static SortedDictionary<string, TcpClient> socket_rastreadores = new SortedDictionary<string, TcpClient>();
        private static ArrayList contas = new ArrayList();
        
        private static void Main()
        {

            #region Contas HERE

            XmlDocument xDoc = new XmlDocument();
            xDoc.Load("END_POINT");

            XmlNodeList coluna = xDoc.GetElementsByTagName("coluna");
            XmlNodeList app_id = xDoc.GetElementsByTagName("app_id");
            XmlNodeList app_code = xDoc.GetElementsByTagName("app_code");
            XmlNodeList inicio = xDoc.GetElementsByTagName("inicio");
            XmlNodeList fim = xDoc.GetElementsByTagName("fim");

            for (int i = 0; i < coluna.Count; i++)
            {
                ArrayList itens = new ArrayList();
                itens.Add(coluna[i].InnerText);
                itens.Add(app_id[i].InnerText);
                itens.Add(app_code[i].InnerText);
                itens.Add(inicio[i].InnerText);
                itens.Add(fim[i].InnerText);
                contas.Add(itens);
            }

            #endregion

            TcpListener socket = new TcpListener(IPAddress.Any, 7014);
            try
            {
                Console.WriteLine("Conectado !");
                socket.Start();
                
                while (true)
                {
                    TcpClient client = socket.AcceptTcpClient();

                    Thread tcpListenThread = new Thread(TcpListenThread);
                    tcpListenThread.Start(client);

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex);
            }
            finally
            {
                Thread tcpListenThread = new Thread(Main);
                tcpListenThread.Start();
                socket.Stop();
            }
        }

        private static void TcpListenThread(object param)
        {
            TcpClient client = (TcpClient)param;
            NetworkStream stream;
            stream = client.GetStream();

            //Thread tcpLSendThread = new Thread(new ParameterizedThreadStart(TcpLSendThread));

            Byte[] bytes = new Byte[99999];
            String mensagem_traduzida;
            string id = "-1";
            int x = 0;
            int i;
            bool from_raster = true;
            stream.ReadTimeout = 1200000;
            try
            {
                List<String> mensagem;
                while (from_raster && (i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    mensagem_traduzida = Encoding.UTF8.GetString(bytes, 0, i);
                    mensagem = mensagem_traduzida.Split(';').ToList();
                    Console.WriteLine("\n" + mensagem_traduzida);

                    //TRATANDO UM COMANDO PARA SIRENE/BLOQUEIO/ODOMETRO/ETC
                    if (mensagem[0].Equals("CLIENTE", StringComparison.InvariantCultureIgnoreCase))
                    {
                        #region COMANDO
                        from_raster = false;
                        NetworkStream sender;
                        if (socket_rastreadores.Keys.Contains(mensagem[1]))
                        {
                            sender = socket_rastreadores[mensagem[1]].GetStream();

                            mensagem.RemoveAt(0);
                            mensagem.RemoveAt(0);
                            Byte[] bytes_send = Encoding.UTF8.GetBytes(String.Join(";", mensagem.ToArray()));
                            sender.Write(bytes_send, 0, bytes_send.Length);
                            stream.ReadTimeout = 1000;
                        }
                        else
                        {
                            Console.WriteLine("\n\n\n KEY NAO EXIST:" + mensagem[1]);
                        }
                        #endregion
                    }
                    else
                    {
                        List<String> dividir_todas_mensagens = mensagem_traduzida.Split('\r').ToList();
                        foreach (string cada in dividir_todas_mensagens)
                        {
                            if (x == 0)
                            {
                                x++;

                                if (cada.Split(';')[1].Equals("Res")){
                                    id = mensagem[2];
                                }
                                else
                                {
                                    id = mensagem[1].Split('\r')[0].Trim();
                                    /*if (mensagem[0].Contains("ALV"))
                                        id = mensagem[1].Split('\r')[0].Trim();
                                    else
                                        id = mensagem[1].Split('\r')[0].Trim();*/
                                }
                                if (socket_rastreadores.ContainsKey(id))
                                {
                                    socket_rastreadores[id] = client;
                                }
                                else
                                {
                                    socket_rastreadores.Add(id, client);
                                }

                            }
                            List<string> enviarList;
                            if (cada.Split(';')[0].Contains("SA200"))
                            {
                                enviarList = cada.Split(';').ToList();
                                enviarList.Insert(2, "0");
                            }
                            else
                            {
                                enviarList = cada.Split(';').ToList();
                            }
                            if (enviarList[0] != "")
                                Interpretar_Msg(enviarList);

                            //if (!tcpLSendThread.IsAlive)
                            //   tcpLSendThread.Start(new Tuple<NetworkStream, string>(stream, mensagem[1]));
                        }
                    }
                }
            }
            catch (Exception)
            {
                //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 12, "Escuta Suntech Novo - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                //Console.WriteLine("\n\n" + e.Message);
                client.Close();
            }
            client.Close();
        }

        private static void Interpretar_Msg(List<String> mensagem)
        {

            string id = "";
            try
            {
                var objeto = new Mensagens();
                if (mensagem[0].Contains("ST4"))
                {
                    #region Mensagem de Localização
                    try
                    {
                        id = mensagem[1];
                        var m = new Mensagens();
                        var r = new Rastreador();
                        r.PorId(id);

                            //MENSAGEM DE POSIÇÃO

                            m.Data_Rastreador = DateTime.Now.ToString("yyyyMMdd HH:mm:ss");
                            //m.Data_Rastreador = mensagem[4] + " " + mensagem[5];
                            m.Data_Gps = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            //m.Data_Gps = mensagem[4].Substring(0, 4) + "-" + mensagem[4].Substring(4, 2) + "-" + mensagem[4].Substring(6, 2) + " " + mensagem[5];
                            m.Data_Recebida = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                            m.ID_Rastreador = id;
                            m.Mensagem = string.Join(";", mensagem);
                            m.Ras_codigo = r.Codigo;
                            m.Tipo_Mensagem ="STT";
                            m.Latitude = "0";
                            m.Longitude = "0";
                            m.Velocidade = "0";
                            //m.Velocidade = Convert.ToDecimal(mensagem[9].Replace('.', ',')).ToString("#0", CultureInfo.InvariantCulture).Replace('.', ',');
                            m.Vei_codigo = r.Vei_codigo != 0 ? r.Vei_codigo : 0;
                            //m.Ignicao = true;
                            m.Ignicao = Convert.ToInt32(mensagem[13]) == 1 ? true : false;
                            //m.Hodometro = "0";
                            m.Hodometro = (Convert.ToInt32(mensagem[16]) / 1000.0).ToString("#0.0", CultureInfo.InvariantCulture).Replace(',', '.');
                            m.Bloqueio = false;
                            //m.Bloqueio = mensagem[15][4] == '1' ? true : false;
                            m.Sirene = false;
                            //m.Sirene = mensagem[15][5] == '1' ? true : false;
                            m.Tensao = "0";
                            //m.Tensao = mensagem[14];
                            m.Horimetro = 0;
                            m.CodAlerta = 0;
                            m.Tipo_Alerta = m.CodAlerta == 0 ? "" : "";
                            m.Endereco = "";// Util.BuscarEndereco(m.Latitude, m.Longitude, contas);

                            #region Gravar
                            if (m.Gravar())
                            {
                                m.Tipo_Mensagem = "EMG";
                                if (r.veiculo != null)
                                {
                                    //Verifica Area de Risco/Cerca
                                    Mensagens.EventoAreaCerca(m);

                                    //Evento Por E-mail
                                    var corpoEmail = m.Tipo_Alerta + "<br /> Endereço: " + m.Endereco;
                                    Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, corpoEmail);
                                }

                                #region Tensão

                                #endregion

                                #region Velocidade
                                /*if (r.Vei_codigo != 0)
                            {
                                var veiculo = Veiculo.BuscarVeiculoVelocidade(m.Vei_codigo);
                                var velocidade_nova = Convert.ToDecimal(veiculo.vei_velocidade);
                                if (velocidade_nova < Convert.ToDecimal(m.Velocidade) && velocidade_nova > 0)
                                {
                                    m.Tipo_Mensagem = "EVT";
                                    m.Tipo_Alerta = "Veículo Ultrapassou a Velocidade";
                                    m.CodAlerta = 23;
                                    m.GravarEvento();

                                    //Evento Por E-mail
                                    var corpoEmail = m.Tipo_Alerta + "<br /> Velocidade: " + m.Velocidade + "<br /> Endereço: " + m.Endereco;
                                    Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, corpoEmail);
                                }
                            }*/
                                #endregion

                            }
                            #endregion

                    }
                    catch (Exception)
                    {

                    }
                    #endregion
                }
                else
                //MENSAGENS ST940
                if (mensagem[1] == "Location" || mensagem[1] == "Emergency" || (mensagem[1] == "RES" && mensagem[0].Contains("ST9")))
                {
                    #region Mensagem de Localização
                    try
                    {
                        id = mensagem[1] == "RES" ? mensagem[3] : mensagem[2];
                        var m = new Mensagens();
                        var r = new Rastreador();
                        r.PorId(id);

                        if(mensagem[1] == "RES" && mensagem[1] == "PRESET"){

                            //MENSAGEM DE COMANDO CONFIGURAÇÃO



                        }else{

                            //MENSAGEM DE POSIÇÃO

                            m.Data_Rastreador = mensagem[4] + " " + mensagem[5];
                            m.Data_Gps = mensagem[4].Substring(0, 4) + "-" + mensagem[4].Substring(4, 2) + "-" + mensagem[4].Substring(6, 2) + " " + mensagem[5];
                            m.Data_Recebida = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                            m.ID_Rastreador = id;
                            m.Mensagem = string.Join(";", mensagem);
                            m.Ras_codigo = r.Codigo;
                            m.Tipo_Mensagem = mensagem[1] == "Location" ? "STT" : "EMG";
                            m.Latitude = mensagem[6];
                            m.Longitude = mensagem[7];
                            m.Velocidade = "0";
                            //m.Velocidade = Convert.ToDecimal(mensagem[9].Replace('.', ',')).ToString("#0", CultureInfo.InvariantCulture).Replace('.', ',');
                            m.Vei_codigo = r.Vei_codigo != 0 ? r.Vei_codigo : 0;
                            m.Ignicao = true;
                            //m.Ignicao = mensagem[15].Count() == 6 ? mensagem[15][0].Equals('0') ? false : true : mensagem[15][8].Equals('0') ? false : true;
                            m.Hodometro = "0";
                            //m.Hodometro = (Convert.ToInt32(mensagem[13]) / 1000.0).ToString("#0.0", CultureInfo.InvariantCulture).Replace(',', '.');
                            m.Bloqueio = false;
                            //m.Bloqueio = mensagem[15][4] == '1' ? true : false;
                            m.Sirene = false;
                            //m.Sirene = mensagem[15][5] == '1' ? true : false;
                            m.Tensao = "0";
                            //m.Tensao = mensagem[14];
                            m.Horimetro = 0;
                            m.CodAlerta = mensagem[1] == "Location" ? 0 : 3;
                            m.Tipo_Alerta = m.CodAlerta == 0 ? "" : "Botão de Pânico Acionado";
                            m.Endereco = Util.BuscarEndereco(m.Latitude, m.Longitude, contas);

                            #region Gravar
                        if (m.Gravar())
                        {
                            m.Tipo_Mensagem = "EMG";
                            if (r.veiculo != null)
                            {
                                //Verifica Area de Risco/Cerca
                                Mensagens.EventoAreaCerca(m);

                                //Evento Por E-mail
                                var corpoEmail = m.Tipo_Alerta + "<br /> Endereço: " + m.Endereco;
                                Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, corpoEmail);
                            }

                            #region Tensão

                            #endregion

                            #region Velocidade
                            /*if (r.Vei_codigo != 0)
                            {
                                var veiculo = Veiculo.BuscarVeiculoVelocidade(m.Vei_codigo);
                                var velocidade_nova = Convert.ToDecimal(veiculo.vei_velocidade);
                                if (velocidade_nova < Convert.ToDecimal(m.Velocidade) && velocidade_nova > 0)
                                {
                                    m.Tipo_Mensagem = "EVT";
                                    m.Tipo_Alerta = "Veículo Ultrapassou a Velocidade";
                                    m.CodAlerta = 23;
                                    m.GravarEvento();

                                    //Evento Por E-mail
                                    var corpoEmail = m.Tipo_Alerta + "<br /> Velocidade: " + m.Velocidade + "<br /> Endereço: " + m.Endereco;
                                    Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, corpoEmail);
                                }
                            }*/
                            #endregion

                        }
                        #endregion

                        }
                    }
                    catch (Exception)
                    {
                        //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 12, "Escuta Suntech Novo - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                        /*StreamWriter txt = new StreamWriter("erros_01.txt", true);
                        txt.WriteLine("ERRO: " + e.Message.ToString());
                        txt.Close();*/
                    }
                    #endregion
                }
                else if (mensagem[0].Contains("STT")) //mensagem proveniente de um STATUS mensagem
                {
                    #region Mensagem de Status
                    try
                    {
                        id = mensagem[1];

                        var m = new Mensagens();
                        var r = new Rastreador();
                        r.PorId(id);

                        m.Data_Rastreador = mensagem[4] + " " + mensagem[5];
                        m.Data_Gps = mensagem[4].Substring(0, 4) + "-" + mensagem[4].Substring(4, 2) + "-" + mensagem[4].Substring(6, 2) + " " + mensagem[5];
                        m.Data_Recebida = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                        m.ID_Rastreador = id;
                        m.Mensagem = string.Join(";", mensagem);
                        m.Ras_codigo = r.Codigo;
                        m.Tipo_Mensagem = "STT";
                        m.Latitude = mensagem[7];
                        m.Longitude = mensagem[8];
                        m.Tipo_Alerta = "";
                        m.Velocidade = Convert.ToDecimal(mensagem[9].Replace('.', ',')).ToString("#0", CultureInfo.InvariantCulture).Replace('.', ',');
                        m.Vei_codigo = r.Vei_codigo != 0 ? r.Vei_codigo : 0;
                        m.Ignicao = mensagem[15].Count() == 6 ? mensagem[15][0].Equals('0') ? false : true : mensagem[15][8].Equals('0') ? false : true;
                        m.Hodometro = (Convert.ToInt32(mensagem[13]) / 1000.0).ToString("#0.0", CultureInfo.InvariantCulture).Replace(',', '.');
                        m.Bloqueio = mensagem[15][4] == '1' ? true : false;
                        m.Sirene = mensagem[15][5] == '1' ? true : false;
                        m.Tensao = mensagem[14];
                        m.Horimetro = 0;
                        m.CodAlerta = 0;
                        m.Endereco = Util.BuscarEndereco(m.Latitude, m.Longitude, contas);

                        #region Gravar
                        if (m.Gravar())
                        {
                            m.Tipo_Mensagem = "EMG";

                            if (r.veiculo != null)
                            {
                                //Verifica Area de Risco/Cerca
                                Mensagens.EventoAreaCerca(m);

                                //Evento Por E-mail
                                var corpoEmail = m.Tipo_Alerta + "<br /> Endereço: " + m.Endereco;
                                Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, corpoEmail);
                            }

                            #region Tensão

                            /*string voltagem = r.veiculo.voltagem.ToString().Replace(",00", "");
                            /*voltagem = voltagem.Length == 3 ? "0" + voltagem : voltagem;
                            string voltagem_correta = voltagem.Substring(0, 2) + "." + voltagem.Substring(2, 2);
                            decimal voltagem_cadastro = Convert.ToDecimal(voltagem_correta);
                            */
                            /*string total = (Convert.ToDecimal(voltagem_correta) + 2).ToString();

                            StreamWriter txt = new StreamWriter("tensao.txt", true);
                            txt.WriteLine("Tensão: " + total);
                            txt.Close();*/

                            //var tet = r.rastreador_evento.Where(x => x.te_codigo.Equals(26)).ToList().ForEach(x => { x.te_codigo });


                            /*var a = r.rastreador_evento.Select(tet => tet.te_codigo = 26);


                            Console.WriteLine("----------------------------");
                            Console.WriteLine(a.ToString());
                            Console.WriteLine("----------------------------");

                            var gravar_evento = true;
                            r.rastreador_evento.ForEach(x => { 
                                if(x.te_codigo == 26){
                                    gravar_evento = false;
                                }
                            });

                            if (gravar_evento)
                            {*/
                                /*if ((Convert.ToDecimal(voltagem_correta) + 200) < Convert.ToDecimal(m.Tensao))
                                {
                                    m.Tipo_Mensagem = "EVT";
                                    m.Tipo_Alerta = "Tensão Acima do Ideal";
                                    m.CodAlerta = 26;
                                    m.GravarEvento();
                                }*/
                            //}

                            /*StreamWriter txt = new StreamWriter("teste_bloqueio_evento.txt", true);
                            txt.WriteLine(tet);
                            txt.Close();*/

                            /*if (!r.rastreador_evento.Where(x => x.te_codigo.Equals(26)))
                            {*/

                            /*decimal porcentagem_alta = voltagem_cadastro + (voltagem_cadastro * Convert.ToDecimal(0.25));
                            /*decimal porcentagem_baixa = voltagem_cadastro - (voltagem_cadastro * Convert.ToDecimal(0.20)); ;

                            if (porcentagem_alta < Convert.ToDecimal(m.Tensao))
                            {
                                m.Tipo_Mensagem = "EVT";
                                m.Tipo_Alerta = "Tensão Acima do Ideal";
                                m.CodAlerta = 26;
                                m.GravarEvento();
                            }*/
                           /*}

                            if (!r.rastreador_evento.Where(x => x.te_codigo.Equals(25)))
                            {
                                StreamWriter txt = new StreamWriter("teste_bloqueio_evento.txt", true);
                                txt.WriteLine("NICE");
                                txt.Close();
                            */
                            /*if (porcentagem_baixa > Convert.ToDecimal(m.Tensao))
                            {
                                m.Tipo_Mensagem = "EVT";
                                m.Tipo_Alerta = "Tensão Abaixo do Ideal";
                                m.CodAlerta = 25;
                                m.GravarEvento();
                            }*/
                            /*}
                            else
                            {
                                StreamWriter txt = new StreamWriter("teste_bloqueio_evento.txt", true);
                                txt.WriteLine("NOT_NICE");
                                txt.Close();
                            }*/


                            #endregion

                            #region Velocidade
                            if (r.Vei_codigo != 0)
                            {
                                var veiculo = Veiculo.BuscarVeiculoVelocidade(m.Vei_codigo);
                                var velocidade_nova = Convert.ToDecimal(veiculo.vei_velocidade);
                                if (velocidade_nova < Convert.ToDecimal(m.Velocidade) && velocidade_nova > 0)
                                {
                                    m.Tipo_Mensagem = "EVT";
                                    m.Tipo_Alerta = "Veículo Ultrapassou a Velocidade";
                                    m.CodAlerta = 23;
                                    m.GravarEvento();

                                    //Evento Por E-mail
                                    var corpoEmail = m.Tipo_Alerta + "<br /> Velocidade: " + m.Velocidade + "<br /> Endereço: " + m.Endereco;
                                    Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, corpoEmail);
                                }
                            }
                            #endregion

                        }
                        #endregion
                    }
                    catch (Exception e)
                    {
                        //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 12, "Escuta Suntech Novo - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                        StreamWriter txt = new StreamWriter("erros_01.txt", true);
                        txt.WriteLine("ERRO: " + e.Message.ToString());
                        txt.Close();
                    }
                    #endregion
                }
                else if (mensagem[0].Contains("EMG")) //mensagem proveniente de uma EMERGÊNCIA
                {
                    #region Mensagem de Emergência
                    try
                    {
                        var r = new Rastreador();
                        var m = new Mensagens();

                        id = mensagem[1];
                        r.PorId(id);

                        m.Data_Gps = mensagem[4].Substring(0, 4) + "-" + mensagem[4].Substring(4, 2) + "-" + mensagem[4].Substring(6, 2) + " " + mensagem[5];
                        m.Data_Recebida = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                        m.ID_Rastreador = id;
                        m.Mensagem = string.Join(";", mensagem);
                        m.Ras_codigo = r.Codigo;
                        m.Tipo_Mensagem = "EMG";
                        m.Latitude = mensagem[7];
                        m.Longitude = mensagem[8];
                        m.Velocidade = Convert.ToDecimal(mensagem[9].Replace('.', ',')).ToString("#0", CultureInfo.InvariantCulture).Replace('.', ',');
                        m.Ignicao = mensagem[15].Count() == 6 ? mensagem[15][0].Equals('0') ? false : true : mensagem[15][8].Equals('0') ? false : true;
                        m.Hodometro = (Convert.ToInt32(mensagem[13]) / 1000.0).ToString("#0.0", CultureInfo.InvariantCulture).Replace(',', '.');
                        m.Bloqueio = mensagem[15][4] == '1' ? true : false;
                        m.Sirene = mensagem[15][5] == '1' ? true : false;
                        m.Tensao = mensagem[14];
                        m.Horimetro = 0;
                        m.Tipo_Alerta = "";
                        //m.Endereco = Mensagens.RequisitarEndereco(m.Latitude, m.Longitude);
                        m.Endereco = Util.BuscarEndereco(m.Latitude, m.Longitude, contas);

                        m.CodAlerta = 0;

                        if (r.veiculo != null)
                        {
                            m.Vei_codigo = r.Vei_codigo;
                        }

                        var grava = false;

                        if (mensagem[16].Equals("2"))
                        {
                            m.Tipo_Alerta = "Parking Lock";
                            m.CodAlerta = 1;
                            grava = true;
                        }
                        else if (mensagem[16].Equals("3"))
                        {
                            m.Tipo_Alerta = "Energia Principal Removida";
                            m.CodAlerta = 2;
                            grava = true;
                        }

                        if (grava) m.Gravar();

                        //Evento Por E-mail
                        var corpoEmail = m.Tipo_Alerta + "<br /> Endereço: " + m.Endereco;
                        Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, corpoEmail);
                    }
                    catch (Exception)
                    {
                        //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 12, "Escuta Suntech Novo - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                        /*StreamWriter txt = new StreamWriter("erros_02.txt", true);
                        txt.WriteLine("ERRO: " + e.Message.ToString());
                        txt.Close();*/
                    }
                    #endregion
                }
                else if (mensagem[0].Contains("EVT")) //mensagem proveniente de um EVENTO
                {
                    #region Mensagem de Evento
                    if (!mensagem[1].Equals("Res"))
                    {
                        #region Evento Não Comando
                        try
                        {
                            id = mensagem[1];
                            var r = new Rastreador();
                            var m = new Mensagens();
                            r.PorId(id);


                            m.Data_Gps = mensagem[4].Substring(0, 4) + "-" + mensagem[4].Substring(4, 2) + "-" + mensagem[4].Substring(6, 2) + " " + mensagem[5];
                            m.Data_Recebida = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                            m.ID_Rastreador = id;
                            m.Mensagem = string.Join(";", mensagem);
                            m.Ras_codigo = r.Codigo;
                            m.Tipo_Mensagem = "EVT";
                            m.Latitude = mensagem[7];
                            m.Longitude = mensagem[8];
                            m.Velocidade = Convert.ToDecimal(mensagem[9].Replace('.', ',')).ToString("#0", CultureInfo.InvariantCulture).Replace('.', ',');
                            m.Ignicao = mensagem[15].Count() == 6 ? mensagem[15][0].Equals('0') ? false : true : mensagem[15][8].Equals('0') ? false : true;
                            m.Hodometro = (Convert.ToInt32(mensagem[13]) / 1000.0).ToString("#0.0", CultureInfo.InvariantCulture).Replace(',', '.');
                            m.Bloqueio = mensagem[15][4] == '1' ? true : false;
                            m.Sirene = mensagem[15][5] == '1' ? true : false;
                            m.Tensao = mensagem[14];
                            m.Horimetro = 0;
                            //m.Endereco = Mensagens.RequisitarEndereco(m.Latitude, m.Longitude);
                            m.Endereco = Util.BuscarEndereco(m.Latitude, m.Longitude, contas);
                            m.CodAlerta = 0;
                            m.Tipo_Alerta = "";

                            #region Eventos
                            if (r.veiculo != null)
                            {
                                m.Vei_codigo = r.Vei_codigo;
                            }

                            var grava = false;
                            /*if (mensagem[16].Equals("2"))
                            {
                                m.Tipo_Alerta = "Botão de Pânico Acionado";
                                m.CodAlerta = 3;
                                grava = true;
                            }
                            else*/ if (mensagem[16].Equals("3")) // entrada 2 desligada
                            {
                                m.Tipo_Alerta = "Sensor Porta Aberta";
                                m.CodAlerta = 4;
                                grava = true;
                            }
                            else if (mensagem[16].Equals("4")) // entrada 2 ligada
                            {
                                m.Tipo_Alerta = "Sensor Porta Fechada";
                                m.CodAlerta = 5;
                                grava = true;
                            }
                            else if (mensagem[16].Equals("5")) // entrada 3 Desligada
                            {
                                m.Tipo_Alerta = "Sensor Plataforma Desativada";
                                m.CodAlerta = 6;
                                grava = true;
                            }
                            else if (mensagem[16].Equals("6")) // entrada 3 Ligada
                            {
                                m.Tipo_Alerta = "Sensor Plataforma Ativada";
                                m.CodAlerta = 7;
                                grava = true;
                            }
                            else if (mensagem[15].Count() == 6)
                            {
                                if (mensagem[15][1].Equals('1'))
                                {
                                    m.Tipo_Alerta = "Sensor Painel Fechado";
                                    m.CodAlerta = 22;
                                    grava = true;
                                }
                                else if (mensagem[15][1].Equals('0'))
                                {
                                    m.Tipo_Alerta = "Sensor Painel Violado";
                                    m.CodAlerta = 21;
                                    grava = true;
                                }
                            }
                            #endregion

                            if (grava) m.Gravar();

                            //Evento Por E-mail
                            var corpoEmail = m.Tipo_Alerta + "<br /> Endereço: " + m.Endereco;
                            Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, corpoEmail);
                        }
                        catch (Exception)
                        {
                            //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 12, "Escuta Suntech Novo - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                            /*StreamWriter txt = new StreamWriter("erros_03_1.txt", true);
                            txt.WriteLine("ERRO: " + e.Message.ToString());
                            txt.Close();*/
                        }                        
                        #endregion
                    }
                    else // Se for um Evento de Comando
                    {
                        #region Evento Comando
                        try
                        {
                            var m = new Mensagens();
                            m.Data_Recebida = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                            m.ID_Rastreador = mensagem[0] == "SA200CMD" ? mensagem[3] : mensagem[2];
                            m.Mensagem = string.Join(";", mensagem);
                            m.Latitude = "+00.0000";
                            m.Longitude = "+000.0000";
                            m.Tipo_Mensagem = "CMD";
                            m.Tipo_Alerta = mensagem[0] == "SA200CMD" ? mensagem[5] : mensagem[4];
                            m.CodAlerta = 0;

                            m.GravarCMD();
                        }
                        catch (Exception)
                        {
                            //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 12, "Escuta Suntech Novo - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                            /*
                            StreamWriter txt = new StreamWriter("erros_03_2.txt", true);
                            txt.WriteLine("ERRO: " + e.Message.ToString());
                            txt.Close();*/
                        }
                        #endregion
                    }
                    #endregion
                }
                else if (mensagem[0].Contains("ALT")) //mensagem proveniente de um ALERT
                {
                    #region Mensagem de um ALERT
                    try
                    {
                        id = mensagem[1];
                        var r = new Rastreador();
                        var m = new Mensagens();
                        r.PorId(id);

                        m.Data_Gps = mensagem[4].Substring(0, 4) + "-" + mensagem[4].Substring(4, 2) + "-" + mensagem[4].Substring(6, 2) + " " + mensagem[5];
                        m.Data_Recebida = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                        m.ID_Rastreador = id;
                        m.Mensagem = string.Join(";", mensagem);
                        m.Ras_codigo = r.Codigo;
                        m.Tipo_Mensagem = "ALT";
                        m.Latitude = mensagem[7];
                        m.Longitude = mensagem[8];
                        m.Velocidade = Convert.ToDecimal(mensagem[9].Replace('.', ',')).ToString("#0", CultureInfo.InvariantCulture).Replace('.', ',');
                        m.Ignicao = mensagem[15].Count() == 6 ? mensagem[15][0].Equals('0') ? false : true : mensagem[15][8].Equals('0') ? false : true;
                        m.Hodometro = (Convert.ToInt32(mensagem[13]) / 1000.0).ToString("#0.0", CultureInfo.InvariantCulture).Replace(',', '.');
                        m.Bloqueio = mensagem[15][4] == '1' ? true : false;
                        m.Sirene = mensagem[15][5] == '1' ? true : false;
                        m.Tensao = mensagem[14];
                        m.Horimetro = 0;
                        m.CodAlerta = 0;
                        m.Tipo_Alerta = "";
                        m.Endereco = Util.BuscarEndereco(m.Latitude, m.Longitude, contas);

                        #region Eventos
                        if (r.veiculo != null)
                        {
                            m.Vei_codigo = r.Vei_codigo;
                        }

                        var grava = false;
                        if (mensagem[16].Equals("3"))
                        {
                            m.Tipo_Alerta = "Antena GPS Desconectada";
                            m.CodAlerta = 8;
                            grava = true;
                        }
                        else if (mensagem[16].Equals("4"))
                        {
                            m.Tipo_Alerta = "Antena GPS Conectada";
                            m.CodAlerta = 9;
                            grava = true;
                        }
                        else if (mensagem[16].Equals("15"))
                        {
                            m.Tipo_Alerta = "Colisão";
                            m.CodAlerta = 10;
                            grava = true;
                        }
                        else if (mensagem[16].Equals("16"))
                        {
                            m.Tipo_Alerta = "Veículo sofreu batida";
                            m.CodAlerta = 11;
                            grava = true;
                        }
                        else if (mensagem[16].Equals("50"))
                        {
                            m.Tipo_Alerta = "Jammer Detectado";
                            m.CodAlerta = 12;
                            grava = true;
                        }
                        #endregion

                        if (grava) m.Gravar();

                        //Evento Por E-mail
                        var corpoEmail = m.Tipo_Alerta + "<br /> Endereço: " + m.Endereco;
                        Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, corpoEmail);
                    }
                    catch (Exception)
                    {
                        //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 12, "Escuta Suntech Novo - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                        /*StreamWriter txt = new StreamWriter("erros_04.txt", true);
                        txt.WriteLine("ERRO: " + e.Message.ToString());
                        txt.Close();*/
                    }
                    #endregion
                }
                else if (mensagem[0].Contains("CMD")) //mensagem proveniente de um COMANDO
                {
                    #region Mensagem de Comando que não for Evento
                    try
                    {
                        var m = new Mensagens();
                        m.Data_Recebida = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                        m.ID_Rastreador = mensagem[0] == "SA200CMD" ? mensagem[3] : mensagem[2];
                        m.Mensagem = string.Join(";", mensagem);
                        m.Latitude = "+00.0000";
                        m.Longitude = "+000.0000";
                        m.Tipo_Mensagem = "CMD";
                        m.Tipo_Alerta = mensagem[0] == "SA200CMD" ? mensagem[5] : mensagem[4];
                        
                        m.GravarCMD();

                    }
                    catch (Exception)
                    {
                        //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 1, "Escuta Suntech Novo - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                        //nada
                        /*StreamWriter txt = new StreamWriter("erros_05.txt", true);
                        txt.WriteLine("ERRO: " + e.Message.ToString());
                        txt.Close();*/
                    }
                    #endregion
                }
            }
            catch (Exception)
            {
                //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 12, "Escuta Suntech Novo - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                //nada
                /*StreamWriter txt = new StreamWriter("erros_06.txt", true);
                txt.WriteLine("ERRO: " + e.Message.ToString());
                txt.Close();*/
            }
        }
    }
}
