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


namespace Monitoramento
{
    class Program
    {
        private static SortedDictionary<string, TcpClient> socket_rastreadores = new SortedDictionary<string, TcpClient>();

        private static void Main()
        {
            TcpListener socket;
            //socket - 7002 - SUNTECH
            //socket - 7005 - SUNTECH ST340/ST350
            //socket - 7007 - SUNTECH ST200
            socket = new TcpListener(IPAddress.Any, 7007);
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
                Console.WriteLine("Erro Conexão: -----" + ex.Message);
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

                                if (cada.Split(';')[1].Equals("Res"))
                                    id = mensagem[2];
                                else
                                {
                                    if (mensagem[0].Contains("ALV"))
                                        id = mensagem[1].Split('\r')[0].Trim();
                                    else
                                        id = mensagem[1].Split('\r')[0].Trim();
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
            catch (Exception e)
            {
                Console.WriteLine("\n\n" + e.Message);
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
                bool gravar = false;
                if (mensagem[0].Contains("STT")) //mensagem proveniente de um STATUS mensagem
                {
                    #region Mensagem de Status
                    try
                    {
                        id = mensagem[1];

                        var m = new Mensagens();
                        var r = new Rastreador();
                        r.PorId(id);

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
                        m.Endereco = Util.BuscarEndereco(m.Latitude, m.Longitude);

                        #region Gravar
                        if (m.Gravar())
                        {
                            m.Tipo_Mensagem = "EMG";
                            if (r.veiculo != null)
                            {
                                #region OLD-CERCA
                                /*
                                #region Areas
                                m.Vei_codigo = r.Vei_codigo;
                                List<Cerca> areas = new Cerca().BuscarAreas("");
                                foreach (Cerca area in areas)
                                {
                                    //esta fora da area
                                    if (area.verifica_fora(m, area))
                                    {
                                        if (r.veiculo.Cer_codigo != 0 && r.veiculo.Cer_codigo == area.Codigo)
                                        {
                                            //Remover da cerca, gravar evento que saiu da area de risco
                                            r.veiculo.Saiu(area.Codigo, area.Area_risco);
                                            m.Tipo_Alerta = "Saiu área de risco '" + area.Descricao + "'";
                                            m.CodAlerta = 16;
                                            gravar = true;
                                        }
                                    }
                                    else // esta dentro
                                    {
                                        if (r.veiculo.Cer_codigo == 0 || r.veiculo.Cer_codigo != area.Codigo)
                                        {
                                            //Insere a cerca no veiculo, garvar evento que entrou na area de risco
                                            r.veiculo.Entrou(area.Codigo, area.Area_risco);
                                            m.Tipo_Alerta = "Entrou área de risco '" + area.Descricao + "'";
                                            m.CodAlerta = 15;
                                            gravar = true;
                                        }
                                    }
                                    if (gravar)
                                    {
                                        m.Gravar();
                                        gravar = false;
                                    }
                                }
                                #endregion

                                #region VeiculoCerca
                                List<Veiculo_Cerca> vcs = new Veiculo_Cerca().porVeiculo(r.veiculo.Codigo);
                                foreach (Veiculo_Cerca vc in vcs)
                                {
                                    try
                                    {
                                        //esta fora da area
                                        if (vc.cerca.verifica_fora(m, vc.cerca))
                                        {
                                            //mas estava dentro
                                            if (vc.dentro)
                                            {
                                                //trocar valor do vc para FORA, gravar evento que saiu na cerca em questao
                                                r.veiculo.Saiu(vc.cerca.Codigo, vc.cerca.Area_risco);
                                                m.Tipo_Alerta = "Saiu da Cerca '" + vc.cerca.Descricao + "'";
                                                m.CodAlerta = 14;
                                                gravar = true;
                                            }
                                        }
                                        else // esta dentro
                                        {
                                            if (!vc.dentro)
                                            {
                                                //trocar valor do vc para DENTRO, gravar evento que Entrou na cerca em questao
                                                r.veiculo.Entrou(vc.cerca.Codigo, vc.cerca.Area_risco);
                                                m.Tipo_Alerta = "Entrou na Cerca '" + vc.cerca.Descricao + "'";
                                                m.CodAlerta = 13;
                                                gravar = true;
                                            }
                                        }
                                        if (gravar)
                                        {
                                            m.Gravar();
                                            gravar = false;
                                        }
                                    }
                                    catch (Exception ex)
                                    {

                                    }
                                }
                                #endregion
                                */
                                #endregion

                                #region Area de Risco
                                var area_risco = Cerca.BuscarAreaRisco();
                                if (area_risco != null)
                                {
                                    foreach (DataRow item in area_risco.Rows)
                                    {
                                        //está dentro da area de risco -> ENTROU
                                        if (!Cerca.VerificaDentroCercaArea(Convert.ToInt32(item["Tipo_cerca"]), item["Posicoes"].ToString(), m.Latitude, m.Longitude))
                                        {
                                            //não estava na cerca
                                            if (!Cerca.VerificaDentroArea(Convert.ToInt32(item["Codigo"]), m.Vei_codigo))
                                            {
                                                //Console.WriteLine("-------> ENTROU");
                                                Cerca.IncluirExcluirVeiculoAreaRiscoCerca(true, true, m.Vei_codigo, Convert.ToInt32(item["Codigo"]));
                                                m.Tipo_Alerta = "Entrou área de risco '" + item["Descricao"] + "'";
                                                m.CodAlerta = 15;
                                                m.GravarEvento();
                                            }
                                        }
                                        //está fora da area de risco -> SAIU
                                        else
                                        {
                                            //não estava na cerca
                                            if (Cerca.VerificaDentroArea(Convert.ToInt32(item["Codigo"]), m.Vei_codigo))
                                            {
                                                //Console.WriteLine("-------> SAIU");
                                                Cerca.IncluirExcluirVeiculoAreaRiscoCerca(false, true, m.Vei_codigo, Convert.ToInt32(item["Codigo"]));
                                                m.Tipo_Alerta = "Saiu área de risco '" + item["Descricao"] + "'";
                                                m.CodAlerta = 16;
                                                m.GravarEvento();
                                            }
                                        }
                                    }
                                }
                                #endregion

                                #region Cercas
                                var cercas_veiculo = Cerca.BuscarCercas(m.Vei_codigo);
                                if (cercas_veiculo != null)
                                {
                                    if (cercas_veiculo.Rows.Count > 0)
                                    {
                                        foreach (DataRow item in cercas_veiculo.Rows)
                                        {
                                            //está dentro da cerca -> ENTROU
                                            if (!Cerca.VerificaDentroCercaArea(Convert.ToInt32(item["Tipo_cerca"]), item["Posicoes"].ToString(), m.Latitude, m.Longitude))
                                            {
                                                if (Convert.ToInt32(item["Dentro"]) == 0)
                                                {
                                                    //Console.WriteLine("-------> ENTROU");
                                                    Cerca.IncluirExcluirVeiculoAreaRiscoCerca(true, false, m.Vei_codigo, Convert.ToInt32(item["Codigo"]));
                                                    m.Tipo_Alerta = "Entrou cerca '" + item["Descricao"] + "'";
                                                    m.CodAlerta = 13;
                                                    m.GravarEvento();
                                                }
                                            }
                                            //está fora da cerca -> SAIU
                                            else
                                            {
                                                if (Convert.ToInt32(item["Dentro"]) == 1)
                                                {
                                                    //Console.WriteLine("-------> SAIU");
                                                    Cerca.IncluirExcluirVeiculoAreaRiscoCerca(false, false, m.Vei_codigo, Convert.ToInt32(item["Codigo"]));
                                                    m.Tipo_Alerta = "Saiu cerca '" + item["Descricao"] + "'";
                                                    m.CodAlerta = 14;
                                                    m.GravarEvento();
                                                }
                                            }
                                        }
                                    }
                                }
                                #endregion
                            }
                        }
                        #endregion

                        //Evento Por E-mail
                        Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, m.Tipo_Alerta);
                    }
                    catch (Exception e)
                    {
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
                        m.Endereco = Util.BuscarEndereco(m.Latitude, m.Longitude);

                        m.CodAlerta = 0;

                        if (r.veiculo != null)
                        {
                            m.Vei_codigo = r.Vei_codigo;
                        }

                        if (mensagem[16].Equals("2"))
                        {
                            m.Tipo_Alerta = "Parking Lock";
                            m.CodAlerta = 1;
                        }
                        else if (mensagem[16].Equals("3"))
                        {
                            m.Tipo_Alerta = "Energia Principal Removida";
                            m.CodAlerta = 2;
                        }

                        m.Gravar();

                        //Evento Por E-mail
                        Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, m.Tipo_Alerta);
                    }
                    catch (Exception e)
                    {
                        StreamWriter txt = new StreamWriter("erros_02.txt", true);
                        txt.WriteLine("ERRO: " + e.Message.ToString());
                        txt.Close();
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
                            m.Endereco = Util.BuscarEndereco(m.Latitude, m.Longitude);
                            m.CodAlerta = 0;
                            m.Tipo_Alerta = "";

                            #region Eventos
                            if (r.veiculo != null)
                            {
                                m.Vei_codigo = r.Vei_codigo;
                            }
                            if (mensagem[16].Equals("2"))
                            {
                                m.Tipo_Alerta = "Botão de Pânico Acionado";
                                m.CodAlerta = 3;
                                gravar = true;
                            }
                            else if (mensagem[16].Equals("3"))//entrada 2 desligada
                            {
                                m.Tipo_Alerta = "Sensor Auxiliar Desligado";
                                m.CodAlerta = 4;
                                gravar = true;
                            }
                            else if (mensagem[16].Equals("4"))//entrada 2 ligada
                            {
                                m.Tipo_Alerta = "Sensor Auxiliar Ligado";
                                m.CodAlerta = 5;
                                gravar = true;
                            }
                            else if (mensagem[16].Equals("5")) // entrada 3 Desligada
                            {
                                m.Tipo_Alerta = "Tomada de Força Desligada";
                                m.CodAlerta = 6;
                                gravar = true;
                            }
                            else if (mensagem[16].Equals("6"))//entrada 3 Ligada
                            {
                                m.Tipo_Alerta = "Tomada de Força Ligada";
                                m.CodAlerta = 7;
                                gravar = true;
                            }
                            #endregion

                            m.Gravar();

                            //Evento Por E-mail
                            Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, m.Tipo_Alerta);
                        }
                        catch (Exception e)
                        {
                            StreamWriter txt = new StreamWriter("erros_03_1.txt", true);
                            txt.WriteLine("ERRO: " + e.Message.ToString());
                            txt.Close();
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
                        catch (Exception e)
                        {
                            StreamWriter txt = new StreamWriter("erros_03_2.txt", true);
                            txt.WriteLine("ERRO: " + e.Message.ToString());
                            txt.Close();
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
                        m.Endereco = Util.BuscarEndereco(m.Latitude, m.Longitude);

                        #region Eventos
                        if (r.veiculo != null)
                        {
                            m.Vei_codigo = r.Vei_codigo;
                        }
                        if (mensagem[16].Equals("3"))
                        {
                            m.Tipo_Alerta = "Antena GPS Desconectada";
                            m.CodAlerta = 8;
                            gravar = true;
                        }
                        else if (mensagem[16].Equals("4"))
                        {
                            m.Tipo_Alerta = "Antena GPS Conectada";
                            m.CodAlerta = 9;
                            gravar = true;
                        }
                        else if (mensagem[16].Equals("15"))
                        {
                            m.Tipo_Alerta = "Colisão";
                            m.CodAlerta = 10;
                            gravar = true;
                        }
                        else if (mensagem[16].Equals("16"))
                        {
                            m.Tipo_Alerta = "Veículo sofreu batida";
                            m.CodAlerta = 11;
                            gravar = true;
                        }
                        else if (mensagem[16].Equals("50"))
                        {
                            m.Tipo_Alerta = "Jammer Detectado";
                            m.CodAlerta = 12;
                            gravar = true;
                        }
                        #endregion

                        m.Gravar();

                        //Evento Por E-mail
                        Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, m.Tipo_Alerta);
                    }
                    catch (Exception e)
                    {
                        StreamWriter txt = new StreamWriter("erros_04.txt", true);
                        txt.WriteLine("ERRO: " + e.Message.ToString());
                        txt.Close();
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
                    catch (Exception e)
                    {
                        //nada
                        StreamWriter txt = new StreamWriter("erros_05.txt", true);
                        txt.WriteLine("ERRO: " + e.Message.ToString());
                        txt.Close();
                    }
                    #endregion
                }
            }
            catch (Exception e)
            {
                //nada
                StreamWriter txt = new StreamWriter("erros_06.txt", true);
                txt.WriteLine("ERRO: " + e.Message.ToString());
                txt.Close();
            }
        }
    }
}
