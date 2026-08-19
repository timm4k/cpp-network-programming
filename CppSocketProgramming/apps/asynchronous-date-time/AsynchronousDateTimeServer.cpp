#include "ApplicationSupport.h"

#include <exception>
#include <iostream>
#include <string>

int main(int argc, char* argv[])
{
    try
    {
        application::configure_console();
        const network::WinsockRuntime winsock;
        const auto port = network::parse_server_port(argc, argv, application::DateTimePort);
        const network::Socket listener = network::create_listener(port);
        const network::NetworkEvent acceptEvent;
        std::cout << "Asynchronous date-time server is listening on 127.0.0.1:" << port << '\n';

        while (true)
        {
            const network::Socket client = network::accept_async(listener, acceptEvent);
            const network::NetworkEvent clientEvent;
            const std::string request = network::receive_line_async(client, clientEvent);
            application::print_received(network::remote_endpoint(client), request);
            if (request == application::ExitCommand)
            {
                break;
            }
            network::send_line_async(client, clientEvent, application::make_date_time_response(request));
        }
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << "Error: " << error.what() << '\n';
        return 1;
    }
}