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
            //socket - 7010 - SUNTECH ST01
            socket = new TcpListener(IPAddress.Any, 7010);
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
                //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 12, "Escuta Suntech Novo - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
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
            catch (Exception ex)
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
                //bool gravar = false;
                if (mensagem[0].Contains("STT")) //mensagem proveniente de um STATUS mensagem
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
                        m.Endereco = Util.BuscarEndereco(m.Latitude, m.Longitude);

                        #region Gravar
                        if (m.Gravar())
                        {
                            m.Tipo_Mensagem = "EMG";
                            if (r.veiculo != null)
                            {
                                Mensagens.EventoAreaCerca(m);

                                //Evento Por E-mail
                                var corpoEmail = m.Tipo_Alerta + "<br /> Endereço: " + m.Endereco;
                                Mensagens.EventoPorEmail(m.Vei_codigo, m.CodAlerta, corpoEmail);
                            }

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
                    catch (Exception ex)
                    {
                        //LogException.GravarException("Erro: " + ex.Message.ToString() + " - Mensagem: " + (ex.InnerException != null ? ex.InnerException.ToString() : " Valor nulo na mensagem "), 12, "Escuta Suntech Novo - Método " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                        /*StreamWriter txt = new StreamWriter("erros_01.txt", true);
                        txt.WriteLine("ERRO: " + e.Message.ToString());
                        txt.Close();*/
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
                    catch (Exception ex)
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
                            m.Endereco = Util.BuscarEndereco(m.Latitude, m.Longitude);
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
                        catch (Exception ex)
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
                        catch (Exception ex)
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
                        m.Endereco = Util.BuscarEndereco(m.Latitude, m.Longitude);

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
                    catch (Exception ex)
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
                    catch (Exception ex)
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
            catch (Exception ex)
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
