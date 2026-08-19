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
        const auto settings = network::parse_client_settings(argc, argv, application::DateTimePort);

        while (true)
        {
            const std::string request = application::read_date_time_command();
            const network::Socket server = network::create_tcp_socket();
            const network::NetworkEvent event;
            network::connect_async(server, event, settings.host, settings.port);
            network::send_line_async(server, event, request);
            if (request == application::ExitCommand)
            {
                break;
            }
            application::print_received(network::remote_endpoint(server), network::receive_line_async(server, event));
        }
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << "Error: " << error.what() << '\n';
        return 1;
    }
}